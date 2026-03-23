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
    /// 파이프라인 목록 관리 및 스텝 구성/파라미터를 편집하는 폼.
    /// PipelineController.ShowEditor()를 통해 다이얼로그로 호출된다.
    /// </summary>
    public partial class PipelineEditorForm : Form
    {
        private readonly List<StepDescriptor> _available;
        private readonly ICogImage            _inputImage;
        private readonly PipelineManager      _pipelineManager;
        private          PipelineConfig       _config;           // 현재 편집 중인 파이프라인
        private          int                  _currentPipelineIdx = -1;

        private Cognex.VisionPro.Display.CogDisplay cogTestDisplay;
        private CogRectangleAffine _testRegion;

        /// <summary>편집 결과 스텝 목록 (OK 클릭 후 유효).</summary>
        public List<IVisionStep> PipelineSteps { get; private set; }

        /// <summary>편집기에서 최종 선택된 파이프라인 인덱스.</summary>
        public int SelectedPipelineIndex => _currentPipelineIdx;

        private IStepParamPanel _currentPanel;
        private int             _currentPanelStepIdx = -1;

        internal PipelineEditorForm(
            List<StepDescriptor> availableSteps,
            PipelineManager      pipelineManager,
            ICogImage            inputImage = null)
        {
            InitializeComponent();
            InitTestDisplay();

            _available       = availableSteps;
            _inputImage      = inputImage;
            _pipelineManager = pipelineManager;

            foreach (var desc in _available)
            {
                if (desc.Category == "Cognex")
                    lstCognex.Items.Add(desc);
                else if (desc.Category == "OpenCV")
                    lstOpenCV.Items.Add(desc);
            }

            RefreshPipelineComboInEditor();

            // 현재 활성 파이프라인으로 초기화
            int initIdx = Math.Max(0, Math.Min(pipelineManager.ActiveIndex, pipelineManager.Configs.Count - 1));
            SwitchToPipeline(initIdx);
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

        // ── 파이프라인 ComboBox 관리 ─────────────────────────────────────

        // 파이프라인별 staged 스텝 목록 — OK 전까지 실제 config에 반영하지 않음
        private readonly Dictionary<int, List<IVisionStep>> _stagedByPipeline
            = new Dictionary<int, List<IVisionStep>>();

        private bool _pipelineComboSyncing;

        private void RefreshPipelineComboInEditor()
        {
            _pipelineComboSyncing = true;
            try
            {
                cmbPipelineSelect.Items.Clear();
                foreach (var cfg in _pipelineManager.Configs)
                    cmbPipelineSelect.Items.Add(cfg.Name);

                if (_currentPipelineIdx >= 0 && _currentPipelineIdx < cmbPipelineSelect.Items.Count)
                    cmbPipelineSelect.SelectedIndex = _currentPipelineIdx;
            }
            finally { _pipelineComboSyncing = false; }
        }

        private void cmbPipelineSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_pipelineComboSyncing) return;
            int idx = cmbPipelineSelect.SelectedIndex;
            if (idx < 0 || idx == _currentPipelineIdx) return;
            StashCurrentStaged();
            SwitchToPipeline(idx);
        }

        private void SwitchToPipeline(int idx)
        {
            if (idx < 0 || idx >= _pipelineManager.Configs.Count) return;
            _currentPipelineIdx = idx;
            _config             = _pipelineManager.Configs[idx];

            // staged가 있으면 재사용, 없으면 실제 config를 deep copy
            List<IVisionStep> staged;
            PipelineSteps = _stagedByPipeline.TryGetValue(idx, out staged)
                ? new List<IVisionStep>(staged)
                : DeepCopySteps(_config.Steps);

            ClearParamPanel();
            RefreshPipelineList();

            _pipelineComboSyncing = true;
            cmbPipelineSelect.SelectedIndex = idx;
            _pipelineComboSyncing = false;
        }

        /// <summary>현재 staged(PipelineSteps)를 dict에 보관한다. 파이프라인 전환 시 호출.</summary>
        private void StashCurrentStaged()
        {
            FlushCurrentPanel();
            if (_currentPipelineIdx >= 0)
                _stagedByPipeline[_currentPipelineIdx] = new List<IVisionStep>(PipelineSteps);
        }

        /// <summary>staged를 현재 _config.Steps에 반영한다. 저장 버튼 전용.</summary>
        private void CommitCurrentToConfig()
        {
            FlushCurrentPanel();
            if (_config != null)
                _config.Steps = new List<IVisionStep>(PipelineSteps);
        }

        /// <summary>모든 staged를 실제 config에 반영한다. OK 버튼 전용.</summary>
        private void ApplyAllStagedToConfigs()
        {
            StashCurrentStaged();   // 현재 파이프라인도 dict에 저장
            foreach (var kv in _stagedByPipeline)
            {
                int idx = kv.Key;
                if (idx >= 0 && idx < _pipelineManager.Configs.Count)
                    _pipelineManager.Configs[idx].Steps = new List<IVisionStep>(kv.Value);
            }
        }

        /// <summary>IStepSerializable을 통해 스텝 목록을 deep copy한다.</summary>
        private List<IVisionStep> DeepCopySteps(List<IVisionStep> source)
        {
            var result = new List<IVisionStep>();
            foreach (var step in source)
            {
                var desc = _available.Find(d => d.TypeName == step.Name);
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
                newStep.DisplayName = step.DisplayName;
                result.Add(newStep);
            }
            return result;
        }

        // ── 파이프라인 CRUD ──────────────────────────────────────────────

        private void btnNewPl_Click(object sender, EventArgs e)
        {
            string name = ShowInputDialog("새 파이프라인", "파이프라인 이름:", "새 파이프라인");
            if (name == null) return;
            StashCurrentStaged();
            _pipelineManager.Add(new PipelineConfig { Name = name });
            RefreshPipelineComboInEditor();
            SwitchToPipeline(_pipelineManager.Configs.Count - 1);
        }

        private void btnDupePl_Click(object sender, EventArgs e)
        {
            StashCurrentStaged();
            string name = ShowInputDialog("파이프라인 복제", "복제할 이름:", _config.Name + " (복사본)");
            if (name == null) return;

            // _config.Steps가 아닌 staged(PipelineSteps)를 복제 기준으로 사용
            var copy = new PipelineConfig { Name = name };
            copy.Steps.AddRange(DeepCopySteps(PipelineSteps));

            _pipelineManager.Add(copy);
            RefreshPipelineComboInEditor();
            SwitchToPipeline(_pipelineManager.Configs.Count - 1);
        }

        private void btnDeletePl_Click(object sender, EventArgs e)
        {
            if (_pipelineManager.Configs.Count <= 1)
            {
                MessageBox.Show("마지막 파이프라인은 삭제할 수 없습니다.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            int removeIdx = _currentPipelineIdx;

            // staged dict 인덱스 재정렬 (삭제된 인덱스 이후 항목을 -1 시프트)
            _stagedByPipeline.Remove(removeIdx);
            var reindexed = new Dictionary<int, List<IVisionStep>>();
            foreach (var kv in _stagedByPipeline)
                reindexed[kv.Key > removeIdx ? kv.Key - 1 : kv.Key] = kv.Value;
            _stagedByPipeline.Clear();
            foreach (var kv in reindexed) _stagedByPipeline[kv.Key] = kv.Value;

            _pipelineManager.RemoveAt(removeIdx);
            int nextIdx = Math.Min(removeIdx, _pipelineManager.Configs.Count - 1);
            RefreshPipelineComboInEditor();
            SwitchToPipeline(nextIdx);
        }

        private void btnRenamePl_Click(object sender, EventArgs e)
        {
            string cur  = _config.Name;
            string name = ShowInputDialog("이름 변경", "새 이름:", cur);
            if (name == null || name == cur) return;
            _config.Name = name;
            RefreshPipelineComboInEditor();
        }

        private static string ShowInputDialog(string title, string prompt, string defaultValue = "")
        {
            var form = new Form
            {
                Text            = title,
                ClientSize      = new Size(380, 105),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition   = FormStartPosition.CenterParent,
                MaximizeBox     = false,
                MinimizeBox     = false,
            };
            var lbl    = new Label  { Text = prompt, Left = 10, Top = 12, AutoSize = true };
            var txt    = new TextBox{ Text = defaultValue, Left = 10, Top = 32, Width = 356 };
            var btnOk  = new Button { Text = "확인", Left = 195, Top = 62, Width = 80, DialogResult = DialogResult.OK };
            var btnCnl = new Button { Text = "취소", Left = 285, Top = 62, Width = 80, DialogResult = DialogResult.Cancel };
            form.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCnl });
            form.AcceptButton = btnOk;
            form.CancelButton = btnCnl;
            txt.SelectAll();
            return form.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(txt.Text)
                ? txt.Text.Trim() : null;
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

        // ── 스텝 순서 목록 갱신 ─────────────────────────────────────────

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
                string label = step.DisplayName != step.Name
                    ? step.DisplayName + "  [" + step.Name + "]"
                    : step.Name;
                lstPipeline.Items.Add((i + 1) + ". " + typeTag.PadRight(16) + label + compat);
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

        private void lstPipeline_DoubleClick(object sender, EventArgs e)
            => btnRemove_Click(sender, e);

        // ── 스텝 드래그&드롭 순서 변경 ──────────────────────────────────

        private int   _dragFromIdx    = -1;
        private int   _dragInsertIdx  = -1;
        private Point _dragStartPoint;
        private bool  _dragPending;

        private void lstPipeline_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            int idx = lstPipeline.IndexFromPoint(e.Location);
            if (idx < 0) { _dragPending = false; return; }
            _dragFromIdx    = idx;
            _dragStartPoint = e.Location;
            _dragPending    = true;
        }

        private void lstPipeline_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragPending || e.Button != MouseButtons.Left) return;
            var sz = SystemInformation.DragSize;
            if (Math.Abs(e.X - _dragStartPoint.X) > sz.Width  / 2 ||
                Math.Abs(e.Y - _dragStartPoint.Y) > sz.Height / 2)
            {
                _dragPending = false;
                lstPipeline.DoDragDrop(_dragFromIdx, DragDropEffects.Move);
            }
        }

        private void lstPipeline_MouseUp(object sender, MouseEventArgs e)
        {
            _dragPending = false;
        }

        private void lstPipeline_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(int)))
            {
                e.Effect = DragDropEffects.None;
                return;
            }
            e.Effect = DragDropEffects.Move;

            var pt        = lstPipeline.PointToClient(new System.Drawing.Point(e.X, e.Y));
            int newInsert = GetDragInsertIndex(pt);
            if (newInsert == _dragInsertIdx) return;

            _dragInsertIdx          = newInsert;
            lstPipeline.InsertIndex = newInsert;
            lstPipeline.Invalidate();
        }

        private void lstPipeline_DragDrop(object sender, DragEventArgs e)
        {
            lstPipeline.InsertIndex = -1;
            lstPipeline.Invalidate();
            int insertIdx  = _dragInsertIdx;
            _dragInsertIdx = -1;

            if (!e.Data.GetDataPresent(typeof(int))) return;

            // dragFrom 제거 후 인덱스 보정
            int effectiveInsert = insertIdx > _dragFromIdx ? insertIdx - 1 : insertIdx;
            if (effectiveInsert == _dragFromIdx) return;

            FlushCurrentPanel();
            var step = PipelineSteps[_dragFromIdx];
            PipelineSteps.RemoveAt(_dragFromIdx);
            PipelineSteps.Insert(effectiveInsert, step);
            _currentPanelStepIdx = -1;
            RefreshPipelineList(effectiveInsert);
        }

        private void lstPipeline_DragLeave(object sender, EventArgs e)
        {
            lstPipeline.InsertIndex = -1;
            lstPipeline.Invalidate();
            _dragInsertIdx = -1;
        }

        // 마우스 Y 위치 기준으로 삽입 위치(0 ~ Count) 계산
        private int GetDragInsertIndex(System.Drawing.Point clientPt)
        {
            int count = lstPipeline.Items.Count;
            for (int i = 0; i < count; i++)
            {
                var rect = lstPipeline.GetItemRectangle(i);
                if (clientPt.Y < rect.Top + rect.Height / 2)
                    return i;
            }
            return count;
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
                _currentPanelStepIdx       = stepIdx;
                txtStepDisplayName.Text    = step.DisplayName;
                txtStepDisplayName.Enabled = true;
                return;
            }

            var panel = ctrl as IStepParamPanel;
            panel?.BindStep(step);

            var blobPanel = ctrl as CogBlobParamPanel;
            if (blobPanel != null)
                blobPanel.PreviewRequested += async (s, ev) =>
                    await RunStepTestAsync(_currentPanelStepIdx);

            if (ctrl is CogWeightedRGBParamPanel weightedRGBPanel)
                weightedRGBPanel.PreviewRequested += async (s, ev) =>
                    await RunStepTestAsync(_currentPanelStepIdx);

            ctrl.Location = new Point(10, 20);
            ctrl.Size     = new Size(grpStepParams.Width - 20, grpStepParams.Height - 30);
            ctrl.Anchor   = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            grpStepParams.Controls.Add(ctrl);

            _currentPanel        = panel;
            _currentPanelStepIdx = stepIdx;

            // 표시 이름 TextBox 활성화
            txtStepDisplayName.Text    = step.DisplayName;
            txtStepDisplayName.Enabled = true;
        }

        private void FlushCurrentPanel()
        {
            if (_currentPanelStepIdx < 0 || _currentPanelStepIdx >= PipelineSteps.Count) return;

            // 표시 이름 반영
            var name = txtStepDisplayName.Text.Trim();
            PipelineSteps[_currentPanelStepIdx].DisplayName =
                string.IsNullOrEmpty(name) ? PipelineSteps[_currentPanelStepIdx].Name : name;

            _currentPanel?.FlushStep(PipelineSteps[_currentPanelStepIdx]);
        }

        private void ClearParamPanel()
        {
            grpStepParams.Controls.Clear();
            _currentPanel               = null;
            _currentPanelStepIdx        = -1;
            txtStepDisplayName.Text     = "";
            txtStepDisplayName.Enabled  = false;
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
                    else if (key.StartsWith("VisionPro.Blob."))
                    {
                        var res = context.Data[key] as CogBlobResults;
                        if (res != null)
                        {
                            int bIdx = 0;
                            int.TryParse(key.Substring("VisionPro.Blob.".Length), out bIdx);
                            var blobs = res.GetBlobs();
                            sb.AppendLine("  Blob[" + bIdx + "]: " + blobs.Count + "개");
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
            if (context.CogImage != null) cogTestDisplay.Image = context.CogImage;

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

            int blobKeyIdx = 0;
            foreach (var key in context.Data.Keys
                .Where(k => k.StartsWith("VisionPro.Blob."))
                .OrderBy(k => k))
            {
                var blobResults = context.Data[key] as CogBlobResults;
                if (blobResults != null)
                {
                    var blobs = blobResults.GetBlobs();
                    sb.AppendLine("Blob[" + blobKeyIdx + "]: " + blobs.Count + "개 검출");
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
                blobKeyIdx++;
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
            CommitCurrentToConfig();
            _pipelineManager.SaveAll();
        }

        private void btnSaveStepParams_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0) return;
            try
            {
                CommitAndSave();
                RefreshPipelineList(idx);
                txtTestResult.Text = "[저장 완료] " + PipelineSteps[idx].DisplayName + "\r\n"
                    + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n" + _pipelineManager.FilePath;
            }
            catch (Exception ex) { txtTestResult.Text = "[저장 실패] " + ex.Message; }
        }

        // ── 확인 / 취소 ──────────────────────────────────────────────────

        private void btnOK_Click(object sender, EventArgs e)
        {
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

            ApplyAllStagedToConfigs();
            _pipelineManager.SaveAll();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    // WM_PAINT 후킹으로 드래그 삽입 위치 선을 안정적으로 유지하는 ListBox
    internal class DragListBox : ListBox
    {
        private const int WM_PAINT = 0x000F;

        public int InsertIndex { get; set; } = -1;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && InsertIndex >= 0)
                DrawInsertLine();
        }

        private void DrawInsertLine()
        {
            if (Items.Count == 0) return;

            int y = InsertIndex >= Items.Count
                ? GetItemRectangle(Items.Count - 1).Bottom
                : GetItemRectangle(InsertIndex).Top;

            using (var g   = CreateGraphics())
            using (var pen = new Pen(Color.DodgerBlue, 2))
            {
                g.DrawLine(pen, 4, y, Width - 4, y);
                // 양 끝 화살표 마커
                g.FillPolygon(pen.Brush, new[]
                {
                    new Point(4,     y),
                    new Point(10,    y - 4),
                    new Point(10,    y + 4),
                });
                g.FillPolygon(pen.Brush, new[]
                {
                    new Point(Width - 4,  y),
                    new Point(Width - 10, y - 4),
                    new Point(Width - 10, y + 4),
                });
            }
        }
    }
}
