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
        private          VisionPipeline       _cachedPipeline;

        /// <summary>현재 로드된 입력 이미지. GetParamPanel의 입력 이미지 목록 구성에 사용.</summary>
        public ICogImage InputImage { get; private set; }

        /// <summary>파라미터 패널 입력 이미지 목록 구성을 위해 로드된 이미지를 등록한다.</summary>
        public void SetInputImage(ICogImage image) { InputImage = image; }

        // ── 공개 속성 ────────────────────────────────────────────────────

        /// <summary>현재 활성 파이프라인의 스텝 목록 (읽기 전용).</summary>
        public IReadOnlyList<IVisionStep> Steps
            => _manager.ActivePipeline?.Steps ?? (IReadOnlyList<IVisionStep>)new List<IVisionStep>();

        /// <summary>관리 중인 파이프라인 설정 목록 (읽기 전용).</summary>
        public IReadOnlyList<PipelineConfig> Pipelines => _manager.Configs;

        /// <summary>
        /// 현재 활성 파이프라인의 인덱스.
        /// 여러 파이프라인이 있을 때 전환에 사용한다.
        /// </summary>
        public int ActivePipelineIndex
        {
            get => _manager.ActiveIndex;
            set { _manager.ActiveIndex = value; RebuildPipeline(); }
        }

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
                    RebuildPipeline();
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
            RebuildPipeline();
        }

        // ── 파이프라인 캐시 ──────────────────────────────────────────────

        /// <summary>
        /// 현재 활성 파이프라인의 스텝으로 VisionPipeline 캐시를 재구성한다.
        /// 스텝 구성이 바뀔 때마다 호출해야 한다.
        /// </summary>
        private void RebuildPipeline()
        {
            _cachedPipeline?.Dispose();
            _cachedPipeline = new VisionPipeline();
            var pipeline = _manager.ActivePipeline;
            if (pipeline != null)
                foreach (var step in pipeline.Steps)
                    _cachedPipeline.AddStep(step);
        }

        // ── 파이프라인 실행 ──────────────────────────────────────────────

        /// <summary>
        /// 현재 활성 파이프라인을 비동기로 실행하고 타입화된 결과를 반환한다.
        ///
        /// 파이프라인이 비어 있으면 VisionResult.Empty를 반환한다.
        /// 각 스텝 내부 예외는 VisionResult.Errors에 기록된다.
        /// </summary>
        /// <param name="image">처리할 입력 이미지 (ICogImage). null 불가.</param>
        /// <returns>
        /// 타입화된 검사 결과.
        /// result.IsSuccess — 오류 없이 완료 여부
        /// result.CaliperEdges — 모든 Caliper 에지 (2D 이미지 좌표)
        /// result.Blobs — Blob 검출 목록
        /// result.Distances — 거리 측정 목록
        /// </returns>
        public async Task<VisionResult> RunAsync(ICogImage image)
        {
            if (image == null) throw new ArgumentNullException("image");

            var pipeline = _manager.ActivePipeline;
            if (pipeline == null || pipeline.Steps.Count == 0)
                return VisionResult.Empty;

            if (_cachedPipeline == null)
                RebuildPipeline();

            using (var ctx = new VisionContext { CogImage = image })
            {
                await _cachedPipeline.RunAsync(ctx);
                return VisionResult.FromContext(ctx, pipeline.Steps);
            }
        }

        // ── 결과 렌더링 ──────────────────────────────────────────────────

        /// <summary>
        /// VisionResult의 모든 결과 그래픽을 CogDisplay에 그린다.
        ///
        /// 그리기 전에 display.StaticGraphics를 자동으로 초기화한다.
        /// 에지 → 초록 십자 마커, Blob → 노란 외곽선, 거리 → 마젠타 선 + 끝점 마커.
        /// </summary>
        /// <param name="display">그릴 대상 Cognex CogDisplay 컨트롤.</param>
        /// <param name="result">렌더링할 검사 결과 (RunAsync 반환값).</param>
        public void DrawResults(Cognex.VisionPro.Display.CogDisplay display, VisionResult result)
        {
            if (display == null || result == null) return;
            display.StaticGraphics.Clear();
            DisplayHelper.DrawAllResults(display, result);
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
                int stepIdx       = IndexOfStep(step);
                var requiredType  = (step as IImageTypedStep)?.RequiredInputType ?? ImageType.Any;
                var available     = BuildAvailableInputImages(stepIdx, requiredType);
                imageSelectable.SetAvailableInputImages(available);
            }

            (ctrl as Vision.UI.IStepParamPanel)?.BindStep(step);
            return ctrl;
        }

        private int IndexOfStep(IVisionStep step)
        {
            var steps = Steps;
            for (int i = 0; i < steps.Count; i++)
                if (steps[i] == step) return i;
            return -1;
        }

        private List<Vision.UI.ImageSourceEntry> BuildAvailableInputImages(int stepIdx, ImageType requiredType)
        {
            var result     = new List<Vision.UI.ImageSourceEntry>();
            var inputType  = InputImage is CogImage24PlanarColor ? ImageType.Color : ImageType.Grey;

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

            var steps = Steps;
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

        // ── 스텝 입력 이미지 ─────────────────────────────────────────────

        /// <summary>
        /// 지정한 타입의 N번째 스텝에 설정된 입력 이미지를 반환한다.
        ///
        /// InputImageKey가 원본 이미지 계열("image:-1", "image:-1.Red/Green/Blue")이면
        /// 파이프라인 실행 없이 즉시 반환한다.
        /// InputImageKey가 처리 스텝 출력("image:N")을 가리키면
        /// 해당 이미지가 생성되기까지의 처리 스텝들을 순서대로 실행한 뒤 반환한다.
        ///
        /// 사용 예:
        ///   _controller.SetInputImage(image);
        ///   var img = _controller.GetStepInputImage&lt;CogCaliperStep&gt;(0);
        ///   var img = _controller.GetStepInputImage&lt;CogBlobStep&gt;(1);
        /// </summary>
        /// <typeparam name="T">입력 이미지를 조회할 스텝 타입.</typeparam>
        /// <param name="index">해당 타입 스텝의 순서 인덱스 (0-based).</param>
        /// <returns>
        /// 스텝의 입력 이미지.
        /// index 범위 초과 또는 InputImage 미설정 시 null.
        /// </returns>
        public ICogImage GetStepInputImage<T>(int index) where T : class, IVisionStep
        {
            if (InputImage == null) return null;

            // 대상 스텝 탐색
            int count = 0;
            IVisionStep targetStep = null;
            foreach (var step in Steps)
            {
                if (!(step is T)) continue;
                if (count++ != index) continue;
                targetStep = step;
                break;
            }
            if (targetStep == null) return null;

            var key = GetInputImageKey(targetStep);

            // 원본 이미지 계열 → 실행 없이 즉시 반환
            var direct = ResolveStaticInputImage(key);
            if (direct != null) return direct;

            // "image:N" → 의존성 체인만 추적하여 필요한 스텝만 실행
            int refStepIdx;
            if (!TryParseStepImageKey(key, out refStepIdx)) return null;

            var context = new VisionContext { CogImage = InputImage };
            context.RegisterImage("image:-1", InputImage);
            var colorSrc = InputImage as CogImage24PlanarColor;
            if (colorSrc != null) context.OriginalColorImage = colorSrc;

            // 필요한 스텝 인덱스만 수집 후 순서대로 실행
            var required = new System.Collections.Generic.SortedSet<int>();
            CollectRequiredSteps(refStepIdx, required);

            var steps = Steps;
            foreach (int i in required)
            {
                context.CurrentStepIndex = i;
                steps[i].Execute(context);
                if (!context.IsSuccess && !steps[i].ContinueOnFailure) break;
            }

            ICogImage result;
            context.Images.TryGetValue(key, out result);
            return result;
        }

        /// <summary>stepIdx 스텝의 출력을 생성하는 데 필요한 스텝 인덱스를 재귀적으로 수집한다.</summary>
        private void CollectRequiredSteps(int stepIdx, System.Collections.Generic.SortedSet<int> required)
        {
            var steps = Steps;
            if (stepIdx < 0 || stepIdx >= steps.Count) return;
            if (required.Contains(stepIdx)) return;
            if (steps[stepIdx] is IInspectionStep) return;

            required.Add(stepIdx);

            // 이 스텝의 InputImageKey가 다른 스텝 출력을 참조하면 재귀적으로 추적
            int depIdx;
            if (TryParseStepImageKey(GetInputImageKey(steps[stepIdx]), out depIdx))
                CollectRequiredSteps(depIdx, required);
        }

        private static string GetInputImageKey(IVisionStep step)
        {
            var cogStep = step as CogStepBase;
            if (cogStep != null) return cogStep.InputImageKey;
            var cvStep = step as CvStepBase;
            if (cvStep != null) return cvStep.InputImageKey;
            return null;
        }

        private ICogImage ResolveStaticInputImage(string key)
        {
            if (string.IsNullOrEmpty(key) || key == "image:-1")
                return InputImage;

            var colorImg = InputImage as CogImage24PlanarColor;
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
            RebuildPipeline();
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
        /// 활성 파이프라인을 복제하여 목록 끝에 추가한다.
        /// 스텝 파라미터(IStepSerializable)도 함께 복사된다.
        /// </summary>
        /// <param name="newName">복제본 이름. null이면 원본 이름 + " (복사본)".</param>
        public void DuplicateActivePipeline(string newName = null)
        {
            var src = _manager.ActivePipeline;
            if (src == null) return;

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
                _manager.Configs[index].Name = newName;
        }

        // ── 내부 유틸 ────────────────────────────────────────────────────

        private void EnsureActivePipeline()
        {
            if (_manager.Configs.Count == 0)
                _manager.Add(new PipelineConfig { Name = "기본 파이프라인" });
        }
    }
}
