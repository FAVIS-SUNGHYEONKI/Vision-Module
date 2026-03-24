using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;
using Cognex.VisionPro.Blob;
using Vision.Steps.VisionPro;
using Vision.Steps.OpenCV;

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

        /// <summary>편집 결과 스텝 목록 (OK 클릭 후 유효).</summary>
        public List<IVisionStep> PipelineSteps { get; private set; }

        /// <summary>편집기에서 최종 선택된 파이프라인 인덱스.</summary>
        public int SelectedPipelineIndex => _currentPipelineIdx;

        private IStepParamPanel _currentPanel;
        private int             _currentPanelStepIdx = -1;
        private VisionContext   _lastRunContext;

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
            btnRunAll.Enabled         = hasImage && PipelineSteps.Count > 0;
            btnShowAllRegions.Enabled = hasImage;
            btnSingleStepTest.Enabled = false; // 스텝 선택 후 활성화

            cogTestDisplay.VerticalScrollBar   = false;
            cogTestDisplay.HorizontalScrollBar = false;

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
            cogTestDisplay.Location = new Point(12, 550);
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

        /// <summary>이미지 키("image:-1", "image:-1.Green", "image:2.Red" 등)의 이미지 타입을 반환한다.</summary>
        private ImageType ResolveImageKeyType(string key)
        {
            if (string.IsNullOrEmpty(key)) return ImageType.Any;
            if (key == "image:-1") return InputImageType;
            if (key.StartsWith("image:-1.")) return ImageType.Grey; // 채널 추출 → Grey
            if (key.StartsWith("image:"))
            {
                var rest = key.Substring("image:".Length);
                if (rest.Contains(".")) return ImageType.Grey; // "N.Red/Green/Blue" → Grey
                if (int.TryParse(rest, out var idx) && idx >= 0 && idx < PipelineSteps.Count)
                    return GetOutputType(PipelineSteps[idx]);
            }
            return ImageType.Any;
        }

        /// <summary>스텝의 InputImageKey를 반환한다. (CogStepBase / CvStepBase 공용)</summary>
        private static string GetStepInputImageKey(IVisionStep step)
        {
            if (step is Vision.Steps.VisionPro.CogStepBase cog) return cog.InputImageKey;
            if (step is Vision.Steps.OpenCV.CvStepBase    cv)  return cv.InputImageKey;
            return null;
        }

        /// <summary>스텝의 InputImageKey를 설정한다. (CogStepBase / CvStepBase 공용)</summary>
        private static void SetStepInputImageKey(IVisionStep step, string key)
        {
            if (step is Vision.Steps.VisionPro.CogStepBase cog) cog.InputImageKey = key;
            else if (step is Vision.Steps.OpenCV.CvStepBase cv) cv.InputImageKey  = key;
        }

        /// <summary>
        /// stepIdx 직전까지 파이프라인을 흘러온 "실질 이미지 타입"을 계산한다.
        /// 검사 스텝(ProducedOutputType=Any)은 이미지를 바꾸지 않으므로 흐름 타입 유지.
        /// </summary>
        private ImageType GetFlowingType(int stepIdx)
        {
            var flowing = InputImageType;
            for (int i = 0; i < stepIdx && i < PipelineSteps.Count; i++)
            {
                var ot = GetOutputType(PipelineSteps[i]);
                if (ot != ImageType.Any) flowing = ot;
            }
            return flowing;
        }

        /// <summary>입력 이미지의 실제 타입을 반환한다. null이면 Any.</summary>
        private ImageType InputImageType
        {
            get
            {
                if (_inputImage == null)                  return ImageType.Any;
                if (_inputImage is CogImage24PlanarColor) return ImageType.Color;
                return ImageType.Grey;
            }
        }

        /// <summary>
        /// stepIdx 스텝 기준으로 사용 가능한 입력 이미지 목록을 정적 분석으로 구성한다.
        /// 원본 이미지 + 이전 스텝 중 이미지를 생산하는 스텝의 출력이 포함된다.
        /// Color 이미지는 .Red/.Green/.Blue 채널 항목도 추가된다.
        /// </summary>
        private List<ImageSourceEntry> GetAvailableInputImages(int stepIdx, ImageType requiredType)
        {
            var result = new List<ImageSourceEntry>();

            void AddEntry(string key, ImageType type, string label)
            {
                if (requiredType != ImageType.Any && type != ImageType.Any && type != requiredType) return;
                result.Add(new ImageSourceEntry { Key = key, Type = type, Label = label });
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

            // 원본 이미지
            AddWithChannels("image:-1", InputImageType, "원본 이미지");

            // 이전 스텝 출력
            for (int i = 0; i < stepIdx; i++)
            {
                var step    = PipelineSteps[i];
                var outType = GetOutputType(step);
                if (outType == ImageType.Any) continue; // pass-through, 이미지 생산 안 함

                string label = "[Step " + (i + 1) + "] " + step.DisplayName;
                string key   = "image:" + i;

                if (step is IMultiChannelStep)
                {
                    // R/G/B 3채널을 개별 항목으로 표시 (기본 단일 항목 없음)
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

        // ── 소스 ListBox 선택 관리 ───────────────────────────────────────


        private void lstCognex_DoubleClick(object sender, EventArgs e) => AddSelectedStep(sender, e);
        private void lstOpenCV_DoubleClick(object sender, EventArgs e) => AddSelectedStep(sender, e);

        // ── 스텝 순서 목록 갱신 ─────────────────────────────────────────

        private void RefreshPipelineList(int selectIdx = -1)
        {
            // 스텝 Name별 누적 카운터 (Caliper.0, Caliper.1 ... 인덱스 계산용)
            var nameCounter = new Dictionary<string, int>();

            lstPipeline.BeginUpdate();
            lstPipeline.Items.Clear();
            for (int i = 0; i < PipelineSteps.Count; i++)
            {
                var step    = PipelineSteps[i];
                var inType  = GetInputType(step);
                var outType = GetOutputType(step);
                string typeTag = outType == ImageType.Any
                    ? "[" + StepDescriptor.TypeLabel(inType) + "]"
                    : "[" + StepDescriptor.TypeLabel(inType) + "->" + StepDescriptor.TypeLabel(outType) + "]";
                // InputImageKey가 있으면 해당 이미지 타입, 없으면 파이프라인 흐름 타입으로 호환성 검사
                var inputKey = GetStepInputImageKey(step);
                bool hasKey  = !string.IsNullOrEmpty(inputKey);
                var effectiveInputType = hasKey ? ResolveImageKeyType(inputKey) : GetFlowingType(i);
                bool compat_ok = IsCompatible(effectiveInputType, inType);
                string compat  = compat_ok ? (i > 0 || hasKey ? "  OK" : "")
                    : "  !! " + StepDescriptor.TypeLabel(effectiveInputType) + "->" + StepDescriptor.TypeLabel(inType) + " 불일치";

                // Name별 순번 계산
                if (!nameCounter.ContainsKey(step.Name)) nameCounter[step.Name] = 0;
                int toolIdx = nameCounter[step.Name]++;
                string nameWithIdx = "[" + step.Name + "." + toolIdx + "]";

                string label = step.DisplayName != step.Name
                    ? step.DisplayName + "  " + nameWithIdx
                    : nameWithIdx;
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
            btnRunAll.Enabled   = _inputImage != null && PipelineSteps.Count > 0;
        }

        // ── 스텝 추가 / 제거 / 순서 변경 ────────────────────────────────

        private void AddSelectedStep(object sender, EventArgs e)
        {
            var desc = (lstCognex.SelectedItem as StepDescriptor)
                    ?? (lstOpenCV.SelectedItem as StepDescriptor);
            if (desc == null) return;

            FlushCurrentPanel();
            var newStep = desc.CreateStep();

            // 원본이미지가 Color이고 스텝이 Grey를 요구하면 기본 InputImageKey를 Green 채널로 설정
            if (desc.RequiredInputType == ImageType.Grey && InputImageType == ImageType.Color)
                SetStepInputImageKey(newStep, "image:-1.Green");

            // Color를 요구하는 스텝은 context.CogImage 오염을 방지하기 위해 InputImageKey 자동 설정
            // (이전 검사 스텝이 context.CogImage를 Grey로 바꿔도 명시적 키로 Color를 조회)
            if (desc.RequiredInputType == ImageType.Color)
            {
                var available = GetAvailableInputImages(PipelineSteps.Count, ImageType.Color);
                if (available.Count > 0)
                    SetStepInputImageKey(newStep, available[0].Key);
            }

            // 호환성 검사: 불일치 시 사용 가능한 이미지가 있으면 자동 선택, 없으면 경고
            {
                var inputKey      = GetStepInputImageKey(newStep);
                var effectiveType = !string.IsNullOrEmpty(inputKey)
                    ? ResolveImageKeyType(inputKey)
                    : GetFlowingType(PipelineSteps.Count);
                if (!IsCompatible(effectiveType, desc.RequiredInputType))
                {
                    // 선택 가능한 호환 이미지가 있으면 첫 번째를 기본 InputImageKey로 자동 설정
                    var available = GetAvailableInputImages(PipelineSteps.Count, desc.RequiredInputType);
                    if (available.Count > 0)
                    {
                        SetStepInputImageKey(newStep, available[0].Key);
                        effectiveType = ResolveImageKeyType(available[0].Key);
                    }
                    // 여전히 불일치하면 경고 다이얼로그
                    if (!IsCompatible(effectiveType, desc.RequiredInputType))
                    {
                        string msg = "[이미지 타입 불일치]\n\n"
                            + "현재 이미지: " + StepDescriptor.TypeLabel(effectiveType) + "\n"
                            + "추가할 스텝 입력: " + StepDescriptor.TypeLabel(desc.RequiredInputType) + "\n\n"
                            + "그래도 추가하시겠습니까?";
                        if (MessageBox.Show(msg, "타입 불일치 경고",
                                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
                            return;
                    }
                }
            }

            PipelineSteps.Add(newStep);
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
            btnShowAllRegions.Enabled  = _inputImage != null;
            btnSingleStepTest.Enabled  = _inputImage != null && idx >= 0;
            btnSaveStepParams.Enabled  = idx >= 0;
        }

        private void ShowParamPanel(int stepIdx)
        {
            FlushCurrentPanel();
            ClearParamPanel();
            if (stepIdx < 0 || stepIdx >= PipelineSteps.Count) return;

            var step = PipelineSteps[stepIdx];
            UpdateDisplayForStep(step);
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

            // 입력 이미지 선택 기능을 지원하는 패널이면 목록 먼저 전달 후 Bind
            var imageSelectable = ctrl as IInputImageSelectable;
            if (imageSelectable != null)
            {
                var inputType = GetInputType(step);
                var available = GetAvailableInputImages(stepIdx, inputType);
                imageSelectable.SetAvailableInputImages(available);
            }

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

            // Image Processing Tool: 초기 선택 시 자동 미리보기 실행 → 변환 결과를 display에 표시
            if (_inputImage != null && GetOutputType(step) != ImageType.Any)
                _ = RunStepTestAsync(stepIdx);
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

        /// <summary>
        /// 이미지 키("image:-1", "image:-1.Green", "image:N" 등)를 _inputImage 또는
        /// _lastRunContext에서 정적으로 해석하여 반환한다. 해석 불가 시 null.
        /// </summary>
        private ICogImage ResolveStaticImage(string key)
        {
            if (string.IsNullOrEmpty(key) || _inputImage == null) return null;
            if (key == "image:-1") return _inputImage;
            var colorImg = _inputImage as CogImage24PlanarColor;
            if (colorImg != null)
            {
                if (key == "image:-1.Red")   return colorImg.GetPlane(CogImagePlaneConstants.Red);
                if (key == "image:-1.Green") return colorImg.GetPlane(CogImagePlaneConstants.Green);
                if (key == "image:-1.Blue")  return colorImg.GetPlane(CogImagePlaneConstants.Blue);
            }
            if (_lastRunContext != null)
            {
                ICogImage img;
                if (_lastRunContext.Images.TryGetValue(key, out img) && img != null) return img;
            }
            return null;
        }

        /// <summary>
        /// 선택된 스텝의 종류에 따라 cogTestDisplay 이미지를 변경하고,
        /// 그래픽을 초기화한 뒤 스텝에 Region이 설정되어 있으면 표시한다.
        /// - Image Processing Tool: 마지막 실행 결과의 출력 이미지
        /// - 검사 Tool: InputImageKey로 해석한 입력 이미지
        /// </summary>
        private void UpdateDisplayForStep(IVisionStep step)
        {
            if (_inputImage == null) return;

            int  stepIdx      = PipelineSteps.IndexOf(step);
            bool isProcessing = GetOutputType(step) != ImageType.Any;

            ICogImage displayImage;
            if (isProcessing)
            {
                // Image Processing Tool → 출력 이미지 (마지막 실행 컨텍스트에서 조회)
                displayImage = null;
                if (stepIdx >= 0 && _lastRunContext != null)
                    _lastRunContext.Images.TryGetValue("image:" + stepIdx, out displayImage);
                if (displayImage == null) displayImage = _inputImage;
            }
            else
            {
                // 검사 Tool → InputImageKey로 해석한 입력 이미지
                var inputKey = GetStepInputImageKey(step);
                displayImage = ResolveStaticImage(inputKey) ?? _inputImage;
            }

            cogTestDisplay.StaticGraphics.Clear();
            cogTestDisplay.InteractiveGraphics.Clear();
            cogTestDisplay.Image = displayImage;
            cogTestDisplay.Fit(false);

            // ── Region이 있으면 InteractiveGraphics에 표시 (드래그 가능) ──
            var regionStep = step as IRegionStep;
            if (regionStep?.Region == null) return;

            var region = regionStep.Region;
            var graphic = new CogRectangleAffine();
            graphic.SetCenterLengthsRotationSkew(
                region.CenterX, region.CenterY,
                region.SideXLength, region.SideYLength,
                region.Rotation, region.Skew);
            graphic.Color            = CogColorConstants.Cyan;
            graphic.GraphicDOFEnable = CogRectangleAffineDOFConstants.All;
            graphic.Interactive      = true;
            graphic.TipText          = ((IVisionStep)regionStep).Name + " 검사 영역 (드래그로 조정)";
            cogTestDisplay.InteractiveGraphics.Add(graphic, "step_region", false);
            regionStep.Region = graphic;
        }

        // ── Region 설정 ──────────────────────────────────────────────────

        private void btnShowAllRegions_Click(object sender, EventArgs e)
        {
            if (_inputImage == null) return;

            FlushCurrentPanel();
            cogTestDisplay.StaticGraphics.Clear();
            cogTestDisplay.InteractiveGraphics.Clear();
            cogTestDisplay.Image = _inputImage;

            int regionCount = 0;
            for (int i = 0; i < PipelineSteps.Count; i++)
            {
                var regionStep = PipelineSteps[i] as IRegionStep;
                if (regionStep == null) continue;

                var saved = regionStep.Region;
                double cx  = saved != null ? saved.CenterX     : _inputImage.Width  / 2.0;
                double cy  = saved != null ? saved.CenterY     : _inputImage.Height / 2.0;
                double w   = saved != null ? saved.SideXLength : _inputImage.Width  / 3.0;
                double h   = saved != null ? saved.SideYLength : _inputImage.Height / 6.0;
                double rot = saved != null ? saved.Rotation    : 0.0;
                double skw = saved != null ? saved.Skew        : 0.0;

                var graphic = new CogRectangleAffine();
                graphic.SetCenterLengthsRotationSkew(cx, cy, w, h, rot, skw);
                graphic.GraphicDOFEnable = CogRectangleAffineDOFConstants.All;
                graphic.Interactive      = true;
                graphic.Color            = CogColorConstants.Cyan;
                graphic.TipText          = "[" + (i + 1) + "] " + PipelineSteps[i].Name + " 영역 (드래그로 조정)";
                cogTestDisplay.InteractiveGraphics.Add(graphic, "region_" + i, false);
                regionStep.Region = graphic;

                regionCount++;
            }

            txtTestResult.Text = regionCount > 0
                ? "전체 " + regionCount + "개 Region 표시 중. 드래그하여 조정한 뒤 저장하세요."
                : "Region을 지원하는 스텝이 없습니다.";
        }

        // ── 스텝 테스트 ──────────────────────────────────────────────────

        private async void btnSingleStepTest_Click(object sender, EventArgs e)
        {
            int idx = lstPipeline.SelectedIndex;
            if (idx < 0 || _inputImage == null) return;
            FlushCurrentPanel();
            await RunStepTestAsync(idx);
        }

        private bool _previewRunning;

        private async Task RunStepTestAsync(int idx)
        {
            if (idx < 0 || idx >= PipelineSteps.Count || _inputImage == null) return;
            if (_previewRunning) return;
            _previewRunning = true;
            btnShowAllRegions.Enabled = false;
            btnSingleStepTest.Enabled       = false;
            txtTestResult.Text       = "테스트 실행 중...";

            bool selectedIsInspection = PipelineSteps[idx] is IInspectionStep;
            var context = new VisionContext { CogImage = _inputImage };
            // 원본 이미지 등록 (R/G/B 채널 포함) — InputImageKey로 채널 조회 가능하도록
            context.RegisterImage("image:-1", _inputImage);
            if (_inputImage is CogImage24PlanarColor colorImg0)
                context.OriginalColorImage = colorImg0;
            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < idx; i++)
                    {
                        if (!selectedIsInspection && PipelineSteps[i] is IInspectionStep) continue;
                        context.CurrentStepIndex = i;
                        PipelineSteps[i].Execute(context);
                        if (!context.IsSuccess && !PipelineSteps[i].ContinueOnFailure) break;
                    }
                    context.CurrentStepIndex = idx;
                    PipelineSteps[idx].Execute(context);
                });
            }
            catch (Exception ex)
            {
                txtTestResult.Text = "[예외] " + ex.Message;
                btnShowAllRegions.Enabled = _inputImage != null;
                btnSingleStepTest.Enabled = true;
                _previewRunning    = false;
                return;
            }

            _lastRunContext = context;
            ShowTestResult(context, PipelineSteps[idx]);
            // Image Processing Tool이면 출력 이미지를 display에 반영
            if (GetOutputType(PipelineSteps[idx]) != ImageType.Any)
                UpdateDisplayForStep(PipelineSteps[idx]);
            btnShowAllRegions.Enabled = _inputImage != null;
            btnSingleStepTest.Enabled       = true;
            _previewRunning          = false;
        }

        // ── 전체 파이프라인 실행 ─────────────────────────────────────────

        private async void btnRunAll_Click(object sender, EventArgs e)
        {
            if (_inputImage == null || PipelineSteps.Count == 0) return;
            FlushCurrentPanel();
            btnRunAll.Enabled        = false;
            btnShowAllRegions.Enabled = false;
            btnSingleStepTest.Enabled       = false;
            txtTestResult.Text       = "전체 실행 중...";
            cogTestDisplay.InteractiveGraphics.Clear();
            cogTestDisplay.StaticGraphics.Clear();

            var context     = new VisionContext { CogImage = _inputImage };
            // 원본 이미지 등록 (R/G/B 채널 포함) — InputImageKey로 채널 조회 가능하도록
            context.RegisterImage("image:-1", _inputImage);
            if (_inputImage is CogImage24PlanarColor colorImg1)
                context.OriginalColorImage = colorImg1;
            var stepResults = new List<Tuple<IVisionStep, bool, List<string>, string, long>>();
            var totalSw = Stopwatch.StartNew();

            try
            {
                await Task.Run(() =>
                {
                    for (int i = 0; i < PipelineSteps.Count; i++)
                    {
                        var step       = PipelineSteps[i];
                        context.CurrentStepIndex = i;
                        var keysBefore = new System.Collections.Generic.HashSet<string>(context.Data.Keys);
                        int errsBefore = context.Errors.Count;
                        var stepSw     = Stopwatch.StartNew();
                        step.Execute(context);
                        stepSw.Stop();
                        var newKeys = new List<string>();
                        foreach (var k in context.Data.Keys)
                            if (!keysBefore.Contains(k)) newKeys.Add(k);
                        bool stepOk  = context.Errors.Count == errsBefore;
                        string errMsg = stepOk ? null
                            : context.Errors.Count > 0
                                ? context.Errors[context.Errors.Count - 1]
                                : "(알 수 없는 오류)";
                        stepResults.Add(Tuple.Create(step, stepOk, newKeys, errMsg, stepSw.ElapsedMilliseconds));
                        if (!context.IsSuccess && !step.ContinueOnFailure) break;
                    }
                });
            }
            catch (Exception ex)
            {
                txtTestResult.Text = "[예외] " + ex.Message;
                btnRunAll.Enabled  = _inputImage != null && PipelineSteps.Count > 0;
                btnSingleStepTest.Enabled = true;
                return;
            }

            totalSw.Stop();
            _lastRunContext = context;
            cogTestDisplay.StaticGraphics.Clear();
            if (context.CogImage != null) cogTestDisplay.Image = context.CogImage;

            double totalMs = totalSw.Elapsed.TotalMilliseconds;
            string totalTimeStr = totalMs >= 1000
                ? totalMs.ToString("F1") + " ms (" + (totalMs / 1000.0).ToString("F2") + " s)"
                : totalMs.ToString("F1") + " ms";

            var sb = new StringBuilder();
            sb.AppendLine("=== 전체 파이프라인 실행 결과 ===");
            sb.AppendLine("Tact Time : " + totalTimeStr);
            sb.AppendLine("스텝 수   : " + PipelineSteps.Count + " / 실행: " + stepResults.Count);
            sb.AppendLine(new string('=', 35));

            for (int i = 0; i < stepResults.Count; i++)
            {
                var t      = stepResults[i];
                var step   = t.Item1; bool ok = t.Item2;
                var keys   = t.Item3; var err = t.Item4;
                long stepMs = t.Item5;
                string stepTimeStr = stepMs >= 1000
                    ? stepMs.ToString("F0") + " ms (" + (stepMs / 1000.0).ToString("F2") + " s)"
                    : stepMs + " ms";

                sb.AppendLine();
                sb.AppendLine("[Step " + (i + 1) + "] " + step.Name + (ok ? " ✓" : " ✗") + "  (" + stepTimeStr + ")");
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
            btnShowAllRegions.Enabled = _inputImage != null;
            btnSingleStepTest.Enabled = lastIdx >= 0;
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

        private void btnSaveAllStepParams_Click(object sender, EventArgs e)
        {
            try
            {
                CommitAndSave();
                int idx = lstPipeline.SelectedIndex;
                RefreshPipelineList(idx);
                txtTestResult.Text = "[전체 저장 완료] " + PipelineSteps.Count + "개 스텝\r\n"
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
