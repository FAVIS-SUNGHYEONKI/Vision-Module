using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;
using Cognex.VisionPro.Blob;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// 파이프라인 스텝 구성 및 각 스텝의 파라미터를 편집하는 폼.
    /// PipelineController.ShowEditor()를 통해 다이얼로그로 호출됩니다.
    /// </summary>
    public partial class PipelineEditorForm : Form
    {
        private readonly List<StepDescriptor> _available;
        private readonly ICogImage            _inputImage;
        private readonly PipelineManager      _pipelineManager;
        private readonly PipelineConfig       _config;

        private Cognex.VisionPro.Display.CogDisplay cogTestDisplay;
        private CogRectangleAffine _testRegion;

        /// <summary>편집 결과 스텝 목록 (OK 클릭 후 유효).</summary>
        public List<IVisionStep> PipelineSteps { get; private set; }

        /// <summary>편집 결과 파이프라인 이름 (OK 클릭 후 유효).</summary>
        public string PipelineName => txtPipelineName.Text.Trim();

        private IStepParamPanel _currentPanel;
        private int             _currentPanelStepIdx = -1;

        internal PipelineEditorForm(
            List<StepDescriptor> availableSteps,
            PipelineConfig       config,
            PipelineManager      pipelineManager,
            ICogImage            inputImage = null)
        {
            InitializeComponent();
            InitTestDisplay();

            _available       = availableSteps;
            _inputImage      = inputImage;
            _pipelineManager = pipelineManager;
            _config          = config;
            PipelineSteps    = new List<IVisionStep>(config.Steps);

            txtPipelineName.Text = config.Name;

            foreach (var desc in _available)
            {
                if (desc.Category == "Cognex")
                    lstCognex.Items.Add(desc);
                else if (desc.Category == "OpenCV")
                    lstOpenCV.Items.Add(desc);
            }

            RefreshPipelineList();
        }

        private void PipelineEditorForm_Load(object sender, EventArgs e)
        {
            bool hasImage = _inputImage != null;
            btnRunAll.Enabled        = hasImage && PipelineSteps.Count > 0;
            btnSetTestRegion.Enabled = hasImage;
            btnTestRun.Enabled       = hasImage;

            if (hasImage)
            {
                cogTestDisplay.Image = _inputImage;
                cogTestDisplay.Fit(true);
            }
        }

        private void InitTestDisplay()
        {
            cogTestDisplay = new Cognex.VisionPro.Display.CogDisplay();
            ((System.ComponentModel.ISupportInitialize)cogTestDisplay).BeginInit();
            cogTestDisplay.Name     = "cogTestDisplay";
            cogTestDisplay.Location = new Point(12, 534);
            cogTestDisplay.Size     = new Size(800, 340);
            cogTestDisplay.TabIndex = 100;
            ((System.ComponentModel.ISupportInitialize)cogTestDisplay).EndInit();
            Controls.Add(cogTestDisplay);
        }

        // ── 이미지 타입 유틸리티 ─────────────────────────────────────────

        private static ImageType GetOutputType(IVisionStep step)
            => (step as IImageTypedStep)?.ProducedOutputType ?? ImageType.Any;

        private static ImageType GetInputType(IVisionStep step)
            => (step as IImageTypedStep)?.RequiredInputType ?? ImageType.Any;

        private static bool IsCompatible(ImageType prevOutput, ImageType nextInput)
        {
            if (prevOutput == ImageType.Any || nextInput == ImageType.Any) return true;
            return prevOutput == nextInput;
        }

        // ── 소스 ListBox 선택 관리 ───────────────────────────────────────

        private void lstCognex_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstCognex.SelectedIndex >= 0) lstOpenCV.SelectedIndex = -1;
            btnAdd.Enabled = lstCognex.SelectedIndex >= 0 || lstOpenCV.SelectedIndex >= 0;
        }

        private void lstOpenCV_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstOpenCV.SelectedIndex >= 0) lstCognex.SelectedIndex = -1;
            btnAdd.Enabled = lstCognex.SelectedIndex >= 0 || lstOpenCV.SelectedIndex >= 0;
        }

        private void lstCognex_DoubleClick(object sender, EventArgs e) => btnAdd_Click(sender, e);
        private void lstOpenCV_DoubleClick(object sender, EventArgs e) => btnAdd_Click(sender, e);

        // ── 파이프라인 목록 갱신 ─────────────────────────────────────────

        private void RefreshPipelineList(int selectIdx = -1)
        {
            lstPipeline.BeginUpdate();
            lstPipeline.Items.Clear();
            for (int i = 0; i < PipelineSteps.Count; i++)
            {
                var step    = PipelineSteps[i];
                var inType  = GetInputType(step);
                var outType = GetOutputType(step);
                string typeTag = "[" + StepDescriptor.TypeLabel(inType) + "->" + StepDescriptor.TypeLabel(outType) + "]";
                string compat  = "";
                if (i > 0)
                {
                    var prevOut = GetOutputType(PipelineSteps[i - 1]);
                    bool ok     = IsCompatible(prevOut, inType);
                    compat = ok ? "  OK"
                        : "  !! " + StepDescriptor.TypeLabel(prevOut) + "->" + StepDescriptor.TypeLabel(inType) + " 불일치";
                }
                lstPipeline.Items.Add((i + 1) + ". " + typeTag.PadRight(16) + step.Name + compat);
            }
            lstPipeline.EndUpdate();
            UpdatePipelineButtonStates();
            if (selectIdx >= 0 && selectIdx < lstPipeline.Items.Count)
                lstPipeline.SelectedIndex = selectIdx;
        }

        private void UpdatePipelineButtonStates()
        {
            int idx = lstPipeline.SelectedIndex;
            btnRemove.Enabled   = idx >= 0;
            btnMoveUp.Enabled   = idx > 0;
            btnMoveDown.Enabled = idx >= 0 && idx < PipelineSteps.Count - 1;
            btnRunAll.Enabled   = _inputImage != null && PipelineSteps.Count > 0;
        }

        // ── 스텝 추가 / 제거 / 순서 변경 ────────────────────────────────

        private void btnAdd_Click(object sender, EventArgs e)
        {
            var desc = (lstCognex.SelectedItem as StepDescriptor)
                    ?? (lstOpenCV.SelectedItem as StepDescriptor);
            if (desc == null) return;

            if (PipelineSteps.Count > 0)
            {
                var prevOut = GetOutputType(PipelineSteps[PipelineSteps.Count - 1]);
                if (!IsCompatible(prevOut, desc.RequiredInputType))
                {
                    string msg = "[이미지 타입 불일치]\n\n"
                        + "이전 스텝 출력 : " + StepDescriptor.TypeLabel(prevOut) + "\n"
                        + "추가할 스텝 입력: " + StepDescriptor.TypeLabel(desc.RequiredInputType) + "\n\n"
                        + "그래도 추가하시겠습니까?";
                    if (MessageBox.Show(msg, "타입 불일치 경고",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                        return;
                }
            }
            FlushCurrentPanel();
            PipelineSteps.Add(desc.CreateStep());
            RefreshPipelineList(PipelineSteps.Count - 1);
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0) return;
            if (idx == _currentPanelStepIdx) ClearParamPanel();
            PipelineSteps.RemoveAt(idx);
            RefreshPipelineList(Math.Min(idx, PipelineSteps.Count - 1));
        }

        private void btnMoveUp_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx <= 0) return;
            FlushCurrentPanel();
            var tmp = PipelineSteps[idx - 1]; PipelineSteps[idx - 1] = PipelineSteps[idx]; PipelineSteps[idx] = tmp;
            _currentPanelStepIdx = -1;
            RefreshPipelineList(idx - 1);
        }

        private void btnMoveDown_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0 || idx >= PipelineSteps.Count - 1) return;
            FlushCurrentPanel();
            var tmp = PipelineSteps[idx + 1]; PipelineSteps[idx + 1] = PipelineSteps[idx]; PipelineSteps[idx] = tmp;
            _currentPanelStepIdx = -1;
            RefreshPipelineList(idx + 1);
        }

        // ── 파라미터 패널 ────────────────────────────────────────────────

        private void lstPipeline_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdatePipelineButtonStates();
            int idx = lstPipeline.SelectedIndex;
            ShowParamPanel(idx);
            bool isRegionStep = idx >= 0 && idx < PipelineSteps.Count && PipelineSteps[idx] is IRegionStep;
            btnSetTestRegion.Enabled  = _inputImage != null && isRegionStep;
            btnSaveStepParams.Enabled = idx >= 0;
        }

        private void ShowParamPanel(int stepIdx)
        {
            FlushCurrentPanel();
            ClearParamPanel();
            if (stepIdx < 0 || stepIdx >= PipelineSteps.Count) return;

            var step = PipelineSteps[stepIdx];
            var ctrl = StepParamPanelFactory.Create(step);

            if (ctrl == null)
            {
                grpStepParams.Controls.Add(new Label
                {
                    Text      = step.Name + "\n\n설정 가능한 파라미터가 없습니다.",
                    Location  = new Point(10, 20),
                    AutoSize  = true,
                    ForeColor = Color.Gray,
                });
                _currentPanelStepIdx = stepIdx;
                return;
            }

            var panel = ctrl as IStepParamPanel;
            panel?.BindStep(step);

            // Blob 트랙바 실시간 미리보기 구독
            var blobPanel = ctrl as CogBlobParamPanel;
            if (blobPanel != null)
                blobPanel.PreviewRequested += async (s, ev) =>
                    await RunStepTestAsync(_currentPanelStepIdx);

            ctrl.Location = new Point(10, 20);
            ctrl.Size     = new Size(grpStepParams.Width - 20, grpStepParams.Height - 30);
            ctrl.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            grpStepParams.Controls.Add(ctrl);

            _currentPanel        = panel;
            _currentPanelStepIdx = stepIdx;
        }

        private void FlushCurrentPanel()
        {
            if (_currentPanel == null || _currentPanelStepIdx < 0
                || _currentPanelStepIdx >= PipelineSteps.Count) return;
            _currentPanel.FlushStep(PipelineSteps[_currentPanelStepIdx]);
        }

        private void ClearParamPanel()
        {
            grpStepParams.Controls.Clear();
            _currentPanel        = null;
            _currentPanelStepIdx = -1;
        }

        // ── Region 설정 ──────────────────────────────────────────────────

        private async void btnSetTestRegion_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0 || _inputImage == null) return;

            var step = PipelineSteps[idx] as IRegionStep;
            if (step == null)
            {
                txtTestResult.Text = PipelineSteps[idx].Name + " 스텝은 Region을 지원하지 않습니다.";
                return;
            }

            FlushCurrentPanel();
            btnSetTestRegion.Enabled = false;
            btnTestRun.Enabled       = false;
            txtTestResult.Text       = "입력 이미지 준비 중...";

            ICogImage stepInput = _inputImage;
            if (idx > 0)
            {
                var ctx = new VisionContext { CogImage = _inputImage };
                try
                {
                    await Task.Run(() =>
                    {
                        for (int i = 0; i < idx; i++)
                        {
                            PipelineSteps[i].Execute(ctx);
                            if (!ctx.IsSuccess && !PipelineSteps[i].ContinueOnFailure) break;
                        }
                    });
                    stepInput = ctx.CogImage ?? _inputImage;
                }
                catch (Exception ex)
                {
                    txtTestResult.Text = "[오류] 이전 스텝 실행 실패:\r\n" + ex.Message;
                    btnSetTestRegion.Enabled = true;
                    btnTestRun.Enabled       = true;
                    return;
                }
            }

            cogTestDisplay.StaticGraphics.Clear();
            cogTestDisplay.InteractiveGraphics.Clear();
            cogTestDisplay.Image = stepInput;

            var saved = step.Region;
            double cx  = saved != null ? saved.CenterX     : stepInput.Width  / 2.0;
            double cy  = saved != null ? saved.CenterY     : stepInput.Height / 2.0;
            double w   = saved != null ? saved.SideXLength : stepInput.Width  / 3.0;
            double h   = saved != null ? saved.SideYLength : stepInput.Height / 6.0;
            double rot = saved != null ? saved.Rotation    : 0.0;
            double skw = saved != null ? saved.Skew        : 0.0;

            _testRegion = new CogRectangleAffine();
            _testRegion.SetCenterLengthsRotationSkew(cx, cy, w, h, rot, skw);
            _testRegion.GraphicDOFEnable = CogRectangleAffineDOFConstants.All;
            _testRegion.Interactive      = true;
            _testRegion.Color            = CogColorConstants.Cyan;
            _testRegion.TipText          = ((IVisionStep)step).Name + " 검사 영역 (드래그로 조정)";
            cogTestDisplay.InteractiveGraphics.Add(_testRegion, "test_region", false);
            step.Region = _testRegion;

            txtTestResult.Text = "Region을 드래그하여 조정한 뒤 [스텝 테스트]를 클릭하세요.";
            btnSetTestRegion.Enabled = true;
            btnTestRun.Enabled       = true;
        }

        // ── 스텝 테스트 ──────────────────────────────────────────────────

        private async void btnTestRun_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0 || _inputImage == null) return;
            await RunStepTestAsync(idx);
        }

        private bool _previewRunning;

        private async Task RunStepTestAsync(int idx)
        {
            if (idx < 0 || idx >= PipelineSteps.Count || _inputImage == null) return;
            if (_previewRunning) return;
            _previewRunning = true;
            btnSetTestRegion.Enabled = false;
            btnTestRun.Enabled       = false;
            txtTestResult.Text       = "테스트 실행 중...";

            // 선택 스텝이 검사 스텝이면 이전 검사 스텝도 실행 (데이터 의존성)
            bool selectedIsInspection = PipelineSteps[idx] is IInspectionStep;
            var context = new VisionContext { CogImage = _inputImage };
            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < idx; i++)
                    {
                        if (!selectedIsInspection && PipelineSteps[i] is IInspectionStep) continue;
                        PipelineSteps[i].Execute(context);
                        if (!context.IsSuccess && !PipelineSteps[i].ContinueOnFailure) break;
                    }
                    PipelineSteps[idx].Execute(context);
                });
            }
            catch (Exception ex)
            {
                txtTestResult.Text = "[예외] " + ex.Message;
                btnSetTestRegion.Enabled = _inputImage != null && PipelineSteps[idx] is IRegionStep;
                btnTestRun.Enabled = true;
                _previewRunning    = false;
                return;
            }

            ShowTestResult(context, PipelineSteps[idx]);
            btnSetTestRegion.Enabled = _inputImage != null && PipelineSteps[idx] is IRegionStep;
            btnTestRun.Enabled       = true;
            _previewRunning          = false;
        }

        // ── 전체 파이프라인 실행 ─────────────────────────────────────────

        private async void btnRunAll_Click(object sender, EventArgs e)
        {
            if (_inputImage == null || PipelineSteps.Count == 0) return;
            FlushCurrentPanel();
            btnRunAll.Enabled        = false;
            btnSetTestRegion.Enabled = false;
            btnTestRun.Enabled       = false;
            txtTestResult.Text       = "전체 실행 중...";
            cogTestDisplay.InteractiveGraphics.Clear();
            cogTestDisplay.StaticGraphics.Clear();

            var context     = new VisionContext { CogImage = _inputImage };
            var stepResults = new List<Tuple<IVisionStep, bool, List<string>, string>>();

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < PipelineSteps.Count; i++)
                    {
                        var step       = PipelineSteps[i];
                        var keysBefore = new System.Collections.Generic.HashSet<string>(context.Data.Keys);
                        int errsBefore = context.Errors.Count;
                        step.Execute(context);
                        var newKeys = new List<string>();
                        foreach (var k in context.Data.Keys)
                            if (!keysBefore.Contains(k)) newKeys.Add(k);
                        bool stepOk  = context.Errors.Count == errsBefore;
                        string errMsg = stepOk ? null
                            : context.Errors.Count > 0
                                ? context.Errors[context.Errors.Count - 1]
                                : "(알 수 없는 오류)";
                        stepResults.Add(Tuple.Create(step, stepOk, newKeys, errMsg));
                        if (!context.IsSuccess && !step.ContinueOnFailure) break;
                    }
                });
            }
            catch (Exception ex)
            {
                txtTestResult.Text = "[예외] " + ex.Message;
                btnRunAll.Enabled  = _inputImage != null && PipelineSteps.Count > 0;
                btnTestRun.Enabled = true;
                return;
            }

            cogTestDisplay.StaticGraphics.Clear();
            if (context.CogImage != null) cogTestDisplay.Image = context.CogImage;

            var sb = new StringBuilder();
            sb.AppendLine("=== 전체 파이프라인 실행 결과 ===");
            sb.AppendLine("스텝 수: " + PipelineSteps.Count + " / 실행: " + stepResults.Count);
            sb.AppendLine(new string('=', 35));

            for (int i = 0; i < stepResults.Count; i++)
            {
                var t    = stepResults[i];
                var step = t.Item1; bool ok = t.Item2;
                var keys = t.Item3; var err = t.Item4;

                sb.AppendLine();
                sb.AppendLine("[Step " + (i + 1) + "] " + step.Name + (ok ? " ✓" : " ✗"));
                if (!ok && err != null) sb.AppendLine("  오류: " + err);

                foreach (var key in keys)
                {
                    sb.AppendLine("  결과키: " + key);

                    if (key.StartsWith("VisionPro.Caliper.") && !key.EndsWith(".Region"))
                    {
                        var res = context.Data[key] as CogCaliperResults;
                        if (res != null)
                        {
                            int cIdx = 0;
                            int.TryParse(key.Substring("VisionPro.Caliper.".Length), out cIdx);
                            sb.AppendLine("  Caliper[" + cIdx + "]: " + res.Count + "개 에지");
                            var region = (step as CogCaliperStep)?.Region;
                            for (int j = 0; j < res.Count; j++)
                            {
                                var r = res[j];
                                sb.AppendLine("    에지 " + (j + 1) + ": "
                                    + DisplayHelper.CaliperResultToXY(r, region)
                                    + " Score=" + r.Score.ToString("F4"));
                                DisplayHelper.DrawEdgeMarkerOnDisplay(cogTestDisplay, r, j, region, "ra_" + cIdx + "_");
                            }
                        }
                    }
                    else if (key == "VisionPro.Blob")
                    {
                        object raw; context.Data.TryGetValue(key, out raw);
                        var res = raw as CogBlobResults;
                        if (res != null)
                        {
                            var blobs = res.GetBlobs();
                            sb.AppendLine("  Blob: " + blobs.Count + "개");
                            for (int j = 0; j < blobs.Count && j < 20; j++)
                            {
                                var b = blobs[j] as CogBlobResult;
                                if (b == null) continue;
                                sb.AppendLine("    Blob " + (j + 1) + ": Area=" + b.Area.ToString("F0")
                                    + " Center=(" + b.CenterOfMassX.ToString("F1") + ", " + b.CenterOfMassY.ToString("F1") + ")");
                                DisplayHelper.DrawBlobMarkerOnDisplay(cogTestDisplay, b, j);
                            }
                            if (blobs.Count > 20) sb.AppendLine("    ... (" + blobs.Count + "개 중 20개만 표시)");
                        }
                    }
                    else if (key.StartsWith("VisionPro.CaliperDistance."))
                    {
                        var dist = context.Data[key] as CaliperDistanceResult;
                        if (dist != null)
                        {
                            int dIdx = 0;
                            int.TryParse(key.Substring("VisionPro.CaliperDistance.".Length), out dIdx);
                            sb.AppendLine("  거리[" + dIdx + "]: Caliper[" + dist.CaliperId_A + "]→["
                                + dist.CaliperId_B + "]  " + dist.Distance.ToString("F2") + " px");
                            sb.AppendLine("    A=(" + dist.X1.ToString("F1") + ", " + dist.Y1.ToString("F1") + ")"
                                + "  B=(" + dist.X2.ToString("F1") + ", " + dist.Y2.ToString("F1") + ")");
                            DisplayHelper.DrawDistanceLineOnDisplay(cogTestDisplay, dist, dIdx);
                        }
                    }
                }
                if (keys.Count == 0 && ok) sb.AppendLine("  (이미지 변환 스텝)");
            }

            if (stepResults.Count < PipelineSteps.Count)
            {
                sb.AppendLine(); sb.AppendLine("--- 미실행 스텝 (파이프라인 중단) ---");
                for (int i = stepResults.Count; i < PipelineSteps.Count; i++)
                    sb.AppendLine("  [Skip] " + PipelineSteps[i].Name);
            }

            txtTestResult.Text = sb.ToString();
            int lastIdx = lstPipeline.SelectedIndex;
            btnSetTestRegion.Enabled = _inputImage != null && lastIdx >= 0 && PipelineSteps[lastIdx] is IRegionStep;
            btnTestRun.Enabled = lastIdx >= 0;
            btnRunAll.Enabled  = true;
        }

        // ── 단일 스텝 테스트 결과 표시 ───────────────────────────────────

        private void ShowTestResult(VisionContext context, IVisionStep step)
        {
            cogTestDisplay.StaticGraphics.Clear();
            if (context.CogImage != null) { cogTestDisplay.Image = context.CogImage; cogTestDisplay.Fit(true); }

            var sb = new StringBuilder();
            if (!context.IsSuccess)
            {
                sb.AppendLine("[실패]");
                foreach (var err in context.Errors) sb.AppendLine("  " + err);
                txtTestResult.Text = sb.ToString();
                return;
            }

            sb.AppendLine("[" + step.Name + "] 성공");
            sb.AppendLine(new string('-', 30));

            int caliperKeyIdx = 0;
            foreach (var key in context.Data.Keys
                .Where(k => k.StartsWith("VisionPro.Caliper.") && !k.EndsWith(".Region"))
                .OrderBy(k => k))
            {
                var results = context.Data[key] as CogCaliperResults;
                if (results == null) { caliperKeyIdx++; continue; }
                sb.AppendLine("Caliper[" + caliperKeyIdx + "]: " + results.Count + "개 에지 검출");
                var caliperStep = step as CogCaliperStep;
                var region = (caliperKeyIdx == 0 && caliperStep != null) ? caliperStep.Region : null;
                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    sb.AppendLine("  에지 " + (i + 1) + ": " + DisplayHelper.CaliperResultToXY(r, region)
                        + " Score=" + r.Score.ToString("F4"));
                    DisplayHelper.DrawEdgeMarkerOnDisplay(cogTestDisplay, r, i, region, "te_" + caliperKeyIdx + "_");
                }
                caliperKeyIdx++;
            }

            object rawBlob;
            if (context.Data.TryGetValue("VisionPro.Blob", out rawBlob))
            {
                var blobResults = rawBlob as CogBlobResults;
                if (blobResults != null)
                {
                    var blobs = blobResults.GetBlobs();
                    sb.AppendLine("Blob: " + blobs.Count + "개 검출");
                    for (int i = 0; i < blobs.Count && i < 20; i++)
                    {
                        var b = blobs[i] as CogBlobResult;
                        if (b == null) continue;
                        sb.AppendLine("  Blob " + (i + 1) + ": Area=" + b.Area.ToString("F0")
                            + " Center=(" + b.CenterOfMassX.ToString("F1") + ", " + b.CenterOfMassY.ToString("F1") + ")");
                        DisplayHelper.DrawBlobMarkerOnDisplay(cogTestDisplay, b, i);
                    }
                    if (blobs.Count > 20) sb.AppendLine("  ... (" + blobs.Count + "개 중 20개만 표시)");
                }
            }

            int distKeyIdx = 0;
            foreach (var key in context.Data.Keys
                .Where(k => k.StartsWith("VisionPro.CaliperDistance."))
                .OrderBy(k => k))
            {
                var dist = context.Data[key] as CaliperDistanceResult;
                if (dist == null) { distKeyIdx++; continue; }
                sb.AppendLine("거리[" + distKeyIdx + "]: Caliper[" + dist.CaliperId_A + "]→["
                    + dist.CaliperId_B + "]  " + dist.Distance.ToString("F2") + " px");
                sb.AppendLine("  A=(" + dist.X1.ToString("F1") + ", " + dist.Y1.ToString("F1") + ")"
                    + "  B=(" + dist.X2.ToString("F1") + ", " + dist.Y2.ToString("F1") + ")");
                DisplayHelper.DrawDistanceLineOnDisplay(cogTestDisplay, dist, distKeyIdx);
                distKeyIdx++;
            }

            if (context.Data.Count == 0)
                sb.AppendLine("(이미지 변환 스텝 — 출력 이미지를 Display에서 확인하세요)");

            txtTestResult.Text = sb.ToString();
        }

        // ── 저장 ─────────────────────────────────────────────────────────

        private void CommitAndSave()
        {
            FlushCurrentPanel();
            if (!string.IsNullOrWhiteSpace(txtPipelineName.Text))
                _config.Name = txtPipelineName.Text.Trim();
            _config.Steps = new List<IVisionStep>(PipelineSteps);
            _pipelineManager.SaveAll();
        }

        private void btnSaveStepParams_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0) return;
            try
            {
                CommitAndSave();
                txtTestResult.Text = "[저장 완료] " + PipelineSteps[idx].Name + "\r\n"
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + _pipelineManager.FilePath;
            }
            catch (Exception ex) { txtTestResult.Text = "[저장 실패] " + ex.Message; }
        }

        private void btnSavePipeline_Click(object sender, EventArgs e)
        {
            try
            {
                CommitAndSave();
                txtTestResult.Text = "[저장 완료] 파이프라인 '" + _config.Name + "' ("
                    + PipelineSteps.Count + "개 스텝)\r\n"
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + _pipelineManager.FilePath;
            }
            catch (Exception ex) { txtTestResult.Text = "[저장 실패] " + ex.Message; }
        }

        // ── 확인 / 취소 ──────────────────────────────────────────────────

        private void btnOK_Click(object sender, EventArgs e)
        {
            FlushCurrentPanel();
            if (string.IsNullOrWhiteSpace(txtPipelineName.Text))
            {
                MessageBox.Show("파이프라인 이름을 입력하세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtPipelineName.Focus();
                return;
            }

            var issues = new StringBuilder();
            for (int i = 1; i < PipelineSteps.Count; i++)
            {
                var prevOut = GetOutputType(PipelineSteps[i - 1]);
                var curIn   = GetInputType(PipelineSteps[i]);
                if (!IsCompatible(prevOut, curIn))
                    issues.AppendLine("  " + i + " -> " + (i + 1) + ":  "
                        + StepDescriptor.TypeLabel(prevOut) + " -> " + StepDescriptor.TypeLabel(curIn)
                        + "  (" + PipelineSteps[i - 1].Name + " -> " + PipelineSteps[i].Name + ")");
            }
            if (issues.Length > 0)
            {
                string msg = "[타입 불일치]\n\n" + issues + "\n계속 진행하시겠습니까?";
                if (MessageBox.Show(msg, "파이프라인 타입 경고",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                    return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }
}
