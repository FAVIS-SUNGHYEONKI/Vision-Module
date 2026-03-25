using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cognex.VisionPro;
using Vision.Steps.VisionPro;
using Vision.Steps.OpenCV;
using Vision.UI;

namespace Vision
{
    /// <summary>
    /// Vision Module의 메인 API 클래스.
    ///
    /// 파이프라인 편집 UI 표시, 파이프라인 실행, 결과 렌더링을 단일 인터페이스로 제공한다.
    /// 외부 프로그램은 이 클래스 하나만 사용하면 Vision Module의 모든 기능을 활용할 수 있다.
    ///
    /// 기본 사용 패턴:
    /// <code>
    /// // 1. 초기화 (설정 폴더 지정)
    /// var controller = new PipelineController(@"C:\MyApp\vision");
    /// controller.Load(); // 이전에 저장한 파이프라인 복원
    ///
    /// // 2. 파이프라인 편집 (내장 GUI 호출)
    /// if (controller.ShowEditor(this, currentImage) == DialogResult.OK)
    ///     controller.Save();
    ///
    /// // 3. 검사 실행
    /// var result = await controller.RunAsync(cogImage);
    ///
    /// // 4. 결과 표시
    /// if (result.IsSuccess)
    ///     controller.DrawResults(cogDisplay, result);
    ///
    /// // 5. 타입화된 결과 접근
    /// foreach (var edge in result.CaliperEdges)
    ///     Console.WriteLine($"X={edge.X:F1}, Y={edge.Y:F1}, Score={edge.Score:F3}");
    /// foreach (var dist in result.Distances)
    ///     Console.WriteLine($"거리: {dist.Distance:F2} px");
    /// </code>
    /// </summary>
    public class PipelineController
    {
        private readonly PipelineManager      _manager;
        private readonly List<StepDescriptor> _stepDescriptors = new List<StepDescriptor>();
        private readonly Dictionary<int, VisionPipeline> _pipelineCache = new Dictionary<int, VisionPipeline>();

        // ── 공개 속성 ────────────────────────────────────────────────────

        /// <summary>관리 중인 파이프라인 설정 목록 (읽기 전용).</summary>
        public IReadOnlyList<PipelineConfig> Pipelines => _manager.Configs;

        /// <summary>
        /// ShowEditor에서 마지막으로 선택된 파이프라인 인덱스 (읽기 전용).
        /// 편집기 OK 후 ComboBox 동기화 등에만 사용한다.
        /// </summary>
        public int ActivePipelineIndex => _manager.ActiveIndex;

        /// <summary>pipelines.xml의 전체 파일 경로.</summary>
        public string ConfigFilePath => _manager.FilePath;

        // ── 생성자 ───────────────────────────────────────────────────────

        /// <summary>
        /// PipelineController를 초기화한다.
        /// 기본 제공 스텝(Cognex Caliper / Blob / ConvertGray / CaliperDistance, OpenCV Threshold)이
        /// 자동으로 편집기 팔레트에 등록된다.
        /// </summary>
        /// <param name="configFolder">
        /// pipelines.xml을 저장/로드할 디렉터리 경로.
        /// 디렉터리가 없으면 Save() 호출 시 자동 생성된다.
        /// </param>
        public PipelineController(string configFolder)
        {
            _manager = new PipelineManager(configFolder);
            RegisterBuiltinSteps();
        }

        // ── 스텝 등록 ────────────────────────────────────────────────────

        private void RegisterBuiltinSteps()
        {
            _stepDescriptors.Add(new StepDescriptor(
                "ConvertGrey (컬러→회색)", "Cognex", () => new CogConvertGrey()));
            _stepDescriptors.Add(new StepDescriptor(
                "WeightedRGB (가중 그레이)", "Cognex", () => new CogWeightedRGBStep()));
            _stepDescriptors.Add(new StepDescriptor(
                "Caliper (에지 검출)",      "Cognex", () => new CogCaliperStep()));
            _stepDescriptors.Add(new StepDescriptor(
                "Blob (영역 검출)",          "Cognex", () => new CogBlobStep()));
            _stepDescriptors.Add(new StepDescriptor(
                "CaliperDistance (거리 측정)", "Cognex", () => new CogCaliperDistanceStep()));
            _stepDescriptors.Add(new StepDescriptor(
                "Threshold (이진화)",        "OpenCV",  () => new CvThresholdStep()));
        }

        /// <summary>
        /// 커스텀 스텝을 편집기 팔레트에 추가한다.
        ///
        /// 외부 프로그램이 자체 IVisionStep 구현체를 등록할 때 사용한다.
        /// 등록된 스텝은 편집기의 Cognex 또는 OpenCV 목록에 나타난다.
        /// </summary>
        /// <param name="displayName">편집기 목록에 표시할 이름 (예: "MyStep").</param>
        /// <param name="category">"Cognex", "OpenCV" 또는 임의 카테고리 문자열.</param>
        /// <param name="factory">호출 시 새 스텝 인스턴스를 반환하는 팩토리 함수.</param>
        public void RegisterStep(string displayName, string category, Func<IVisionStep> factory)
            => _stepDescriptors.Add(new StepDescriptor(displayName, category, factory));

        // ── 편집기 표시 ──────────────────────────────────────────────────

        /// <summary>
        /// 파이프라인 편집 다이얼로그를 열어 스텝 구성과 파라미터를 수정한다.
        ///
        /// DialogResult.OK이면 편집 내용이 메모리에 반영된다.
        /// 파일에 영구 저장하려면 Save()를 추가로 호출한다.
        /// 파이프라인이 하나도 없으면 "기본 파이프라인"을 자동으로 생성한다.
        /// </summary>
        /// <param name="owner">부모 창 핸들 (null이면 데스크톱에 표시).</param>
        /// <param name="inputImage">
        /// 편집기 내 테스트(단일 스텝 실행 / 전체 실행)에 사용할 입력 이미지.
        /// null이면 테스트 기능이 비활성화된다.
        /// </param>
        /// <returns>사용자 선택 결과: DialogResult.OK 또는 DialogResult.Cancel.</returns>
        public DialogResult ShowEditor(IWin32Window owner = null, ICogImage inputImage = null)
        {
            EnsureActivePipeline();

            // 폼 열기 전 상태를 인메모리 XML로 스냅샷 — Cancel 시 복원 기준점
            var snapshot = TakeSnapshot();

            using (var form = new PipelineEditorForm(
                _stepDescriptors,
                _manager,
                inputImage))
            {
                var dr = form.ShowDialog(owner);
                if (dr == DialogResult.OK)
                {
                    _manager.ActiveIndex = form.SelectedPipelineIndex;
                    InvalidatePipelineCache();
                }
                else
                {
                    // Cancel(또는 X) — 스냅샷으로 복원 후 파일에도 기록
                    RestoreSnapshot(snapshot);
                }
                return dr;
            }
        }

        // ── 스냅샷 ───────────────────────────────────────────────────────

        private System.Xml.Linq.XElement TakeSnapshot()
        {
            var root = new System.Xml.Linq.XElement("Pipelines",
                new System.Xml.Linq.XAttribute("active", _manager.ActiveIndex));

            foreach (var cfg in _manager.Configs)
            {
                var cfgEl   = new System.Xml.Linq.XElement("Pipeline",
                    new System.Xml.Linq.XAttribute("name", cfg.Name));
                var stepsEl = new System.Xml.Linq.XElement("Steps");

                foreach (var step in cfg.Steps)
                {
                    var stepEl = new System.Xml.Linq.XElement("Step",
                        new System.Xml.Linq.XAttribute("type", step.Name));
                    if (step.DisplayName != step.Name)
                        stepEl.Add(new System.Xml.Linq.XAttribute("label", step.DisplayName));
                    (step as IStepSerializable)?.SaveParams(stepEl);
                    stepsEl.Add(stepEl);
                }

                cfgEl.Add(stepsEl);
                root.Add(cfgEl);
            }

            return root;
        }

        private void RestoreSnapshot(System.Xml.Linq.XElement snapshot)
        {
            var factories = new Dictionary<string, Func<IVisionStep>>();
            foreach (var desc in _stepDescriptors)
                factories[desc.TypeName] = desc.CreateStep;

            _manager.LoadFromElement(snapshot, factories);
            _manager.SaveAll();   // 폼에서 중간 저장된 파일도 이전 상태로 덮어씀
            InvalidatePipelineCache();
        }

        // ── 파이프라인 캐시 ──────────────────────────────────────────────

        /// <summary>모든 파이프라인 캐시를 비운다. 스텝 구성 변경 시 호출한다.</summary>
        private void InvalidatePipelineCache()
        {
            foreach (var vp in _pipelineCache.Values)
                vp.Dispose();
            _pipelineCache.Clear();
        }

        /// <summary>지정 인덱스의 VisionPipeline을 캐시에서 가져오거나 새로 빌드한다.</summary>
        private VisionPipeline GetOrBuildPipeline(int pipelineIndex)
        {
            VisionPipeline cached;
            if (_pipelineCache.TryGetValue(pipelineIndex, out cached))
                return cached;

            var config = pipelineIndex >= 0 && pipelineIndex < _manager.Configs.Count
                ? _manager.Configs[pipelineIndex] : null;

            var vp = new VisionPipeline();
            if (config != null)
                foreach (var step in config.Steps)
                    vp.AddStep(step);

            _pipelineCache[pipelineIndex] = vp;
            return vp;
        }

        // ── 파이프라인 실행 ──────────────────────────────────────────────

        /// <summary>
        /// 지정한 파이프라인을 비동기로 실행하고 타입화된 결과를 반환한다.
        ///
        /// 파이프라인이 비어 있으면 VisionResult.Empty를 반환한다.
        /// 각 스텝 내부 예외는 VisionResult.Errors에 기록된다.
        /// </summary>
        /// <param name="image">처리할 입력 이미지 (ICogImage). null 불가.</param>
        /// <param name="pipelineIndex">실행할 파이프라인 인덱스 (0-based).</param>
        /// <returns>
        /// 타입화된 검사 결과.
        /// result.IsSuccess — 오류 없이 완료 여부
        /// result.CaliperEdges — 모든 Caliper 에지 (2D 이미지 좌표)
        /// result.Blobs — Blob 검출 목록
        /// result.Distances — 거리 측정 목록
        /// </returns>
        public async Task<VisionResult> RunAsync(int pipelineIndex)
        {
            if (pipelineIndex < 0 || pipelineIndex >= _manager.Configs.Count)
                return VisionResult.Empty;

            var config = _manager.Configs[pipelineIndex];
            if (config.InputImage == null)
                throw new InvalidOperationException(
                    $"Pipeline[{pipelineIndex}] InputImage가 설정되지 않았습니다. " +
                    "Pipelines[index].SetInputImage(image)를 먼저 호출하세요.");
            if (config.Steps.Count == 0)
                return VisionResult.Empty;

            var vp = GetOrBuildPipeline(pipelineIndex);
            using (var ctx = BuildContext(pipelineIndex))
            {
                await vp.RunAsync(ctx);
                return VisionResult.FromContext(ctx, config.Steps);
            }
        }

        // ── 입력 이미지 목록 / 선택 ─────────────────────────────────────

        /// <summary>
        /// 지정한 스텝에서 선택 가능한 입력 이미지 목록을 반환한다.
        ///
        /// 반환 목록을 ComboBox 등에 바인딩하고, 사용자가 선택한 항목의 Key를
        /// SetStepInputImageKey()에 전달하면 된다.
        ///
        /// 사용 예:
        ///   var entries = controller.GetAvailableInputImages(step);
        ///   comboBox.DataSource  = entries;
        ///   comboBox.DisplayMember = "Label";
        ///   comboBox.ValueMember   = "Key";
        /// </summary>
        /// <param name="step">입력 이미지 목록을 조회할 스텝.</param>
        /// <returns>선택 가능한 입력 이미지 항목 목록. 스텝을 찾을 수 없으면 빈 목록.</returns>
        public IReadOnlyList<Vision.UI.ImageSourceEntry> GetAvailableInputImages(IVisionStep step)
        {
            int pipelineIdx, stepIdx;
            if (!TryFindStep(step, out pipelineIdx, out stepIdx))
                return new List<Vision.UI.ImageSourceEntry>();

            var requiredType = (step as IImageTypedStep)?.RequiredInputType ?? ImageType.Any;
            return BuildAvailableInputImages(pipelineIdx, stepIdx, requiredType);
        }

        /// <summary>
        /// ImageSourceEntry.Key 문자열에 해당하는 실제 ICogImage를 반환한다.
        ///
        /// "image:-1" / "image:-1.Red/Green/Blue" → 파이프라인 실행 없이 즉시 반환.
        /// "image:N" → N번 스텝 출력을 생성하는 데 필요한 처리 스텝만 실행 후 반환.
        ///
        /// 사용 예:
        ///   var entries = controller.GetAvailableInputImages(step);
        ///   comboBox.DataSource    = entries;
        ///   comboBox.DisplayMember = "Label";
        ///   comboBox.ValueMember   = "Key";
        ///   // 콤보 선택 시:
        ///   var key = (string)comboBox.SelectedValue;
        ///   int pipelineIndex = 0;
        ///   ICogImage img = controller.ResolveInputImage(key, pipelineIndex);
        ///   cogDisplay.Image = img;
        /// </summary>
        /// <param name="key">ImageSourceEntry.Key (예: "image:-1", "image:0", "image:-1.Green").</param>
        /// <param name="pipelineIndex">"image:N" 키 처리 시 참조할 파이프라인 인덱스.</param>
        /// <returns>해당 이미지. InputImage 미설정이거나 키가 유효하지 않으면 null.</returns>
        public ICogImage ResolveInputImage(string key, int pipelineIndex)
        {
            if (pipelineIndex < 0 || pipelineIndex >= _manager.Configs.Count) return null;
            if (_manager.Configs[pipelineIndex].InputImage == null) return null;

            var direct = ResolveStaticInputImage(key, pipelineIndex);
            if (direct != null) return direct;

            int refStepIdx;
            if (!TryParseStepImageKey(key, out refStepIdx)) return null;
            if (pipelineIndex < 0 || pipelineIndex >= _manager.Configs.Count) return null;

            var ctx      = BuildContext(pipelineIndex);
            var required = new System.Collections.Generic.SortedSet<int>();
            CollectRequiredSteps(refStepIdx, required, pipelineIndex);
            ExecuteSteps(ctx, required, pipelineIndex);

            ICogImage result;
            ctx.Images.TryGetValue(key, out result);
            return result;
        }

        // ── 파라미터 패널 ────────────────────────────────────────────────

        /// <summary>
        /// 스텝에 맞는 파라미터 편집 UserControl을 반환한다.
        ///
        /// 반환된 Control은 IStepParamPanel도 구현한다.
        /// 외부 폼의 Panel에 Add한 뒤 사용자가 값을 수정하고,
        /// ApplyParamPanel() 또는 ApplyAndSave()로 반영/저장한다.
        ///
        /// 사용 예:
        ///   var ctrl = controller.GetParamPanel(step);
        ///   ctrl.Dock = DockStyle.Fill;
        ///   myGroupBox.Controls.Add(ctrl);
        ///   // 저장 버튼 클릭 시:
        ///   controller.ApplyAndSave(step, (IStepParamPanel)ctrl);
        /// </summary>
        /// <param name="step">파라미터를 편집할 스텝 인스턴스.</param>
        /// <returns>
        /// 스텝에 맞는 IStepParamPanel UserControl.
        /// 해당 패널이 없으면 null을 반환한다.
        /// </returns>
        public Control GetParamPanel(IVisionStep step)
        {
            var ctrl = StepParamPanelFactory.Create(step);

            var imageSelectable = ctrl as Vision.UI.IInputImageSelectable;
            if (imageSelectable != null)
            {
                int pipelineIdx, stepIdx;
                if (TryFindStep(step, out pipelineIdx, out stepIdx))
                {
                    var requiredType = (step as IImageTypedStep)?.RequiredInputType ?? ImageType.Any;
                    var available    = BuildAvailableInputImages(pipelineIdx, stepIdx, requiredType);
                    imageSelectable.SetAvailableInputImages(available);
                }
            }

            (ctrl as Vision.UI.IStepParamPanel)?.BindStep(step);
            return ctrl;
        }

        /// <summary>
        /// 스텝 인스턴스가 속한 파이프라인 인덱스와 스텝 인덱스를 반환한다.
        /// 모든 파이프라인을 탐색한다.
        /// </summary>
        private bool TryFindStep(IVisionStep step, out int pipelineIdx, out int stepIdx)
        {
            var configs = _manager.Configs;
            for (int p = 0; p < configs.Count; p++)
            {
                var steps = configs[p].Steps;
                for (int s = 0; s < steps.Count; s++)
                {
                    if (steps[s] == step)
                    {
                        pipelineIdx = p;
                        stepIdx     = s;
                        return true;
                    }
                }
            }
            pipelineIdx = -1;
            stepIdx     = -1;
            return false;
        }

        private List<Vision.UI.ImageSourceEntry> BuildAvailableInputImages(
            int pipelineIndex, int stepIdx, ImageType requiredType)
        {
            var result    = new List<Vision.UI.ImageSourceEntry>();
            var inputImg  = _manager.Configs[pipelineIndex].InputImage;
            var inputType = inputImg is CogImage24PlanarColor ? ImageType.Color : ImageType.Grey;

            void AddEntry(string key, ImageType type, string label)
            {
                if (requiredType != ImageType.Any && type != ImageType.Any && type != requiredType) return;
                result.Add(new Vision.UI.ImageSourceEntry { Key = key, Type = type, Label = label });
            }

            void AddWithChannels(string key, ImageType type, string label)
            {
                AddEntry(key, type, label);
                if (type == ImageType.Color)
                {
                    AddEntry(key + ".Red",   ImageType.Grey, label + " — Red");
                    AddEntry(key + ".Green", ImageType.Grey, label + " — Green");
                    AddEntry(key + ".Blue",  ImageType.Grey, label + " — Blue");
                }
            }

            AddWithChannels("image:-1", inputType, "원본 이미지");

            var steps = _manager.Configs[pipelineIndex].Steps;
            for (int i = 0; i < stepIdx && i < steps.Count; i++)
            {
                var s       = steps[i];
                var outType = (s as IImageTypedStep)?.ProducedOutputType ?? ImageType.Any;
                if (outType == ImageType.Any) continue;

                string label = "[Step " + (i + 1) + "] " + s.DisplayName;
                string key   = "image:" + i;

                if (s is IMultiChannelStep)
                {
                    AddEntry(key + ".Red",   ImageType.Grey, label + " — Red");
                    AddEntry(key + ".Green", ImageType.Grey, label + " — Green");
                    AddEntry(key + ".Blue",  ImageType.Grey, label + " — Blue");
                }
                else
                {
                    AddWithChannels(key, outType, label);
                }
            }

            return result;
        }

        /// <summary>
        /// 파라미터 패널의 값을 스텝에 반영한다. 디스크 저장은 하지 않는다.
        ///
        /// 실시간 미리보기처럼 값을 즉시 적용해야 할 때 사용한다.
        /// 영구 저장이 필요하면 추가로 Save()를 호출한다.
        /// </summary>
        /// <param name="step">반영 대상 스텝.</param>
        /// <param name="panel">GetParamPanel()이 반환한 패널 (IStepParamPanel 캐스트).</param>
        public void ApplyParamPanel(IVisionStep step, Vision.UI.IStepParamPanel panel)
            => panel?.FlushStep(step);

        /// <summary>
        /// 파라미터 패널의 값을 스텝에 반영하고 ConfigFilePath에 저장한다.
        ///
        /// 외부 앱의 "저장" 버튼 핸들러에서 호출하는 것을 권장한다.
        /// </summary>
        /// <param name="step">반영 대상 스텝.</param>
        /// <param name="panel">GetParamPanel()이 반환한 패널 (IStepParamPanel 캐스트).</param>
        public void ApplyAndSave(IVisionStep step, Vision.UI.IStepParamPanel panel)
        {
            panel?.FlushStep(step);
            _manager.SaveAll();
        }

        // ── 스텝 단위 Image / Result API ────────────────────────────────

        /// <summary>
        /// 지정한 스텝에 설정된 입력 이미지를 반환한다.
        ///
        /// InputImageKey가 원본 이미지 계열("image:-1", "image:-1.Red/Green/Blue")이면
        /// 파이프라인 실행 없이 즉시 반환한다.
        /// InputImageKey가 처리 스텝 출력("image:N")을 가리키면 해당 이미지를 생성하는 데
        /// 필요한 처리 스텝만 의존성 순서대로 실행한 뒤 반환한다.
        /// </summary>
        public ICogImage GetStepInputImage(IVisionStep step)
        {
            if (step == null) return null;

            int pipelineIdx, stepIdx;
            if (!TryFindStep(step, out pipelineIdx, out stepIdx)) return null;
            if (_manager.Configs[pipelineIdx].InputImage == null) return null;

            var key    = GetInputImageKey(step);
            var direct = ResolveStaticInputImage(key, pipelineIdx);
            if (direct != null) return direct;

            int refStepIdx;
            if (!TryParseStepImageKey(key, out refStepIdx)) return null;

            var ctx      = BuildContext(pipelineIdx);
            var required = new System.Collections.Generic.SortedSet<int>();
            CollectRequiredSteps(refStepIdx, required, pipelineIdx);
            ExecuteSteps(ctx, required, pipelineIdx);

            ICogImage result;
            ctx.Images.TryGetValue(key, out result);
            return result;
        }

        /// <summary>
        /// 지정한 처리 스텝(WeightedRGB, ConvertGrey, Threshold 등)이 생산하는 출력 이미지를 반환한다.
        /// 검사 스텝이면 null을 반환한다.
        /// 내부적으로 RunStep을 호출한다.
        /// </summary>
        public ICogImage GetStepOutputImage(IVisionStep step)
            => RunStep(step).OutputImage;

        /// <summary>
        /// 지정한 스텝을 실행하고 입력 이미지, 출력 이미지, 검사 결과를 포함한
        /// StepResult를 반환한다.
        ///
        /// 스텝의 InputImageKey가 처리 스텝 출력을 참조하는 경우 해당 의존 스텝을
        /// 먼저 실행한다 (전체 파이프라인이 아닌 필요한 스텝만).
        ///
        /// 사용 예:
        ///   _controller.SetInputImage(image);
        ///   var result = _controller.RunStep(_controller.Steps[2]);
        ///   cogDisplay.Image = result.InputImage;
        /// </summary>
        public StepResult RunStep(IVisionStep step)
        {
            if (step == null)
                return StepResult.Failure("step이 null입니다.");

            int pipelineIdx, stepIdx;
            if (!TryFindStep(step, out pipelineIdx, out stepIdx))
                return StepResult.Failure("파이프라인에 등록되지 않은 스텝입니다.");
            if (_manager.Configs[pipelineIdx].InputImage == null)
                return StepResult.Failure($"Pipeline[{pipelineIdx}] InputImage가 설정되지 않았습니다.");

            var ctx = BuildContext(pipelineIdx);

            // 의존 처리 스텝 실행
            var inputKey = GetInputImageKey(step);
            int refStepIdx;
            if (TryParseStepImageKey(inputKey, out refStepIdx))
            {
                var required = new System.Collections.Generic.SortedSet<int>();
                CollectRequiredSteps(refStepIdx, required, pipelineIdx);
                ExecuteSteps(ctx, required, pipelineIdx);

                if (!ctx.IsSuccess)
                    return StepResult.Failure(
                        ctx.Errors.Count > 0 ? ctx.Errors[ctx.Errors.Count - 1] : "선행 스텝 실패");
            }

            // 스텝 실행 전 입력 이미지 캡처
            ICogImage inputImg = null;
            if (!string.IsNullOrEmpty(inputKey))
                ctx.Images.TryGetValue(inputKey, out inputImg);
            if (inputImg == null)
                inputImg = ResolveStaticInputImage(inputKey, pipelineIdx) ?? ctx.CogImage;

            // 스텝 실행
            int errsBefore = ctx.Errors.Count;
            ctx.CurrentStepIndex = stepIdx;
            step.Execute(ctx);

            bool stepOk = ctx.Errors.Count == errsBefore;
            string error = stepOk ? null : ctx.Errors[ctx.Errors.Count - 1];

            // 출력 이미지
            ICogImage outputImg = null;
            ctx.Images.TryGetValue("image:" + stepIdx, out outputImg);

            // 검사 결과 (VisionResult.FromContext 재활용)
            var vr = VisionResult.FromContext(ctx, new List<IVisionStep> { step });

            return new StepResult
            {
                IsSuccess    = stepOk,
                Error        = error,
                InputImage   = inputImg,
                OutputImage  = outputImg,
                CaliperEdges = vr.CaliperEdges,
                Blobs        = vr.Blobs,
                Distances    = vr.Distances,
            };
        }

        // ── 내부 공통 실행 헬퍼 ─────────────────────────────────────────

        private VisionContext BuildContext(int pipelineIndex)
        {
            var img = _manager.Configs[pipelineIndex].InputImage;
            var ctx = new VisionContext { CogImage = img };
            ctx.RegisterImage("image:-1", img);
            var colorSrc = img as CogImage24PlanarColor;
            if (colorSrc != null) ctx.OriginalColorImage = colorSrc;
            return ctx;
        }

        private void ExecuteSteps(VisionContext ctx,
            System.Collections.Generic.IEnumerable<int> stepIndices, int pipelineIndex)
        {
            var steps = _manager.Configs[pipelineIndex].Steps;
            foreach (int i in stepIndices)
            {
                ctx.CurrentStepIndex = i;
                steps[i].Execute(ctx);
                if (!ctx.IsSuccess && !steps[i].ContinueOnFailure) break;
            }
        }

        /// <summary>stepIdx 스텝의 출력을 생성하는 데 필요한 스텝 인덱스를 재귀적으로 수집한다.</summary>
        private void CollectRequiredSteps(int stepIdx,
            System.Collections.Generic.SortedSet<int> required, int pipelineIndex)
        {
            var steps = _manager.Configs[pipelineIndex].Steps;
            if (stepIdx < 0 || stepIdx >= steps.Count) return;
            if (required.Contains(stepIdx)) return;
            if (steps[stepIdx] is IInspectionStep) return;

            required.Add(stepIdx);

            int depIdx;
            if (TryParseStepImageKey(GetInputImageKey(steps[stepIdx]), out depIdx))
                CollectRequiredSteps(depIdx, required, pipelineIndex);
        }

        private static string GetInputImageKey(IVisionStep step)
        {
            var cogStep = step as CogStepBase;
            if (cogStep != null) return cogStep.InputImageKey;
            var cvStep = step as CvStepBase;
            if (cvStep != null) return cvStep.InputImageKey;
            return null;
        }

        private ICogImage ResolveStaticInputImage(string key, int pipelineIndex)
        {
            var img = _manager.Configs[pipelineIndex].InputImage;
            if (string.IsNullOrEmpty(key) || key == "image:-1") return img;

            var colorImg = img as CogImage24PlanarColor;
            if (colorImg != null)
            {
                if (key == "image:-1.Red")   return colorImg.GetPlane(CogImagePlaneConstants.Red);
                if (key == "image:-1.Green") return colorImg.GetPlane(CogImagePlaneConstants.Green);
                if (key == "image:-1.Blue")  return colorImg.GetPlane(CogImagePlaneConstants.Blue);
            }

            return null;
        }

        private static bool TryParseStepImageKey(string key, out int stepIdx)
        {
            stepIdx = -1;
            if (string.IsNullOrEmpty(key) || !key.StartsWith("image:")) return false;
            return int.TryParse(key.Substring("image:".Length), out stepIdx) && stepIdx >= 0;
        }

        // ── 저장 / 로드 ──────────────────────────────────────────────────

        /// <summary>
        /// 현재 파이프라인 구성을 ConfigFilePath에 저장한다.
        /// 대상 디렉터리가 없으면 자동으로 생성한다.
        /// </summary>
        public void Save() => _manager.SaveAll();

        /// <summary>
        /// ConfigFilePath에서 파이프라인 구성을 로드한다.
        /// 파일이 없으면 아무 작업도 하지 않는다.
        ///
        /// 등록된 모든 스텝(기본 + RegisterStep으로 추가한 것)의 팩토리가
        /// 자동으로 로드 시 사용된다.
        /// </summary>
        public void Load()
        {
            var factories = new Dictionary<string, Func<IVisionStep>>();
            foreach (var desc in _stepDescriptors)
                factories[desc.TypeName] = desc.CreateStep;
            _manager.LoadAll(factories);
            InvalidatePipelineCache();
        }

        // ── 다중 파이프라인 관리 ─────────────────────────────────────────

        /// <summary>
        /// 새 파이프라인을 추가하고 활성 인덱스를 해당 파이프라인으로 이동한다.
        /// </summary>
        /// <param name="name">파이프라인 이름. 비어 있으면 "Pipeline"을 사용한다.</param>
        public void AddPipeline(string name = "Pipeline")
        {
            _manager.Add(new PipelineConfig
            {
                Name = string.IsNullOrWhiteSpace(name) ? "Pipeline" : name
            });
        }

        /// <summary>
        /// 지정 인덱스 파이프라인을 복제하여 목록 끝에 추가한다.
        /// 스텝 파라미터(IStepSerializable)도 함께 복사된다.
        /// </summary>
        /// <param name="sourceIndex">복제 원본 파이프라인 인덱스 (0-based).</param>
        /// <param name="newName">복제본 이름. null이면 원본 이름 + " (복사본)".</param>
        public void DuplicatePipeline(int sourceIndex, string newName = null)
        {
            if (sourceIndex < 0 || sourceIndex >= _manager.Configs.Count) return;
            var src = _manager.Configs[sourceIndex];

            var copyName = newName ?? (src.Name + " (복사본)");
            var copy     = new PipelineConfig { Name = copyName };

            foreach (var step in src.Steps)
            {
                var desc = _stepDescriptors.Find(d => d.TypeName == step.Name);
                if (desc == null) continue;
                var newStep   = desc.CreateStep();
                var srcSerial = step    as IStepSerializable;
                var dstSerial = newStep as IStepSerializable;
                if (srcSerial != null && dstSerial != null)
                {
                    var el = new System.Xml.Linq.XElement("Step");
                    srcSerial.SaveParams(el);
                    dstSerial.LoadParams(el);
                }
                copy.Steps.Add(newStep);
            }

            _manager.Add(copy);
        }

        /// <summary>
        /// 지정 인덱스의 파이프라인을 삭제한다.
        /// 파이프라인이 하나뿐이면 삭제하지 않는다.
        /// </summary>
        /// <param name="index">삭제할 파이프라인 인덱스.</param>
        /// <returns>삭제 성공 여부. 마지막 항목이면 false.</returns>
        public bool RemovePipeline(int index)
        {
            if (_manager.Configs.Count <= 1) return false;
            _manager.RemoveAt(index);
            return true;
        }

        /// <summary>
        /// 지정 인덱스 파이프라인의 이름을 변경한다.
        /// </summary>
        /// <param name="index">이름을 변경할 파이프라인 인덱스.</param>
        /// <param name="newName">새 이름.</param>
        public void RenamePipeline(int index, string newName)
        {
            if (index < 0 || index >= _manager.Configs.Count) return;
            if (!string.IsNullOrWhiteSpace(newName))
                _manager.RenamePipeline(index, newName);
        }

        // ── 내부 유틸 ────────────────────────────────────────────────────

        private void EnsureActivePipeline()
        {
            if (_manager.Configs.Count == 0)
                _manager.Add(new PipelineConfig { Name = "기본 파이프라인" });
        }
    }
}
