using System;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;
using Cognex.VisionPro.Blob;

namespace Vision.UI
{
    /// <summary>
    /// CogBlobStep의 파라미터를 편집하는 UserControl.
    /// 임계값 트랙바 조작 시 PreviewRequested 이벤트를 발생시켜 실시간 미리보기를 지원합니다.
    /// </summary>
    public class CogBlobParamPanel : UserControl, IStepParamPanel
    {
        private ComboBox      _cmbMode;
        private ComboBox      _cmbPolarity;
        private NumericUpDown _nudMinPixels;

        private Label    _lblHardThresh;
        private TrackBar _trkHardThresh;
        private Label    _lblHardVal;

        private Label    _lblSoftLow;
        private TrackBar _trkSoftLow;
        private Label    _lblSoftLowVal;
        private Label    _lblSoftHigh;
        private TrackBar _trkSoftHigh;
        private Label    _lblSoftHighVal;

        private CogBlobStep _boundStep;
        private bool        _binding;

        /// <summary>트랙바 조작 시 발생 — PipelineEditorForm이 구독하여 미리보기를 실행합니다.</summary>
        public event EventHandler PreviewRequested;

        private static readonly CogBlobSegmentationModeConstants[] Modes =
        {
            CogBlobSegmentationModeConstants.HardFixedThreshold,
            CogBlobSegmentationModeConstants.SoftFixedThreshold,
            CogBlobSegmentationModeConstants.HardRelativeThreshold,
            CogBlobSegmentationModeConstants.SoftRelativeThreshold,
        };
        private static readonly string[] ModeNames =
        {
            "Hard Fixed (고정 임계값)",
            "Soft Fixed (상하한 임계값)",
            "Hard Relative (상대 임계값)",
            "Soft Relative (상대 상하한)",
        };

        private static readonly CogBlobSegmentationPolarityConstants[] Polarities =
        {
            CogBlobSegmentationPolarityConstants.LightBlobs,
            CogBlobSegmentationPolarityConstants.DarkBlobs,
        };
        private static readonly string[] PolarityNames =
        {
            "LightBlobs (밝은 객체)",
            "DarkBlobs (어두운 객체)",
        };

        public CogBlobParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX   = 8;
            const int LblW   = 110;
            const int TrkX   = 122;
            const int TrkW   = 185;
            const int ValX   = 310;
            const int ValW   = 42;
            const int ComboX = 165;
            const int ComboW = 170;
            const int RowH   = 32;
            const int TrkH   = 28;
            int y = 8;

            AddLabel("세그멘테이션 모드:", LblX, y + 4, LblW + 45);
            _cmbMode = new ComboBox
            {
                Location      = new Point(ComboX, y),
                Size          = new Size(ComboW, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbMode.Items.AddRange(ModeNames);
            _cmbMode.SelectedIndexChanged += (s, e) => UpdateThresholdVisibility();
            Controls.Add(_cmbMode);
            y += RowH;

            AddLabel("극성 (Polarity):", LblX, y + 4, LblW + 45);
            _cmbPolarity = new ComboBox
            {
                Location      = new Point(ComboX, y),
                Size          = new Size(ComboW, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbPolarity.Items.AddRange(PolarityNames);
            Controls.Add(_cmbPolarity);
            y += RowH;

            _lblHardThresh = AddLabel("임계값 (Hard):", LblX, y + 5, LblW);
            _trkHardThresh = MakeTrackBar(TrkX, y, TrkW, TrkH, 0, 255, 128);
            _lblHardVal    = AddValueLabel(ValX, y + 5, ValW, "128");
            _trkHardThresh.ValueChanged += (s, e) =>
            { _lblHardVal.Text = _trkHardThresh.Value.ToString(); OnThresholdChanged(); };
            y += TrkH + 4;

            _lblSoftLow    = AddLabel("임계값 Low:", LblX, y + 5, LblW);
            _trkSoftLow    = MakeTrackBar(TrkX, y, TrkW, TrkH, 0, 255, 100);
            _lblSoftLowVal = AddValueLabel(ValX, y + 5, ValW, "100");
            _trkSoftLow.ValueChanged += (s, e) =>
            { _lblSoftLowVal.Text = _trkSoftLow.Value.ToString(); OnThresholdChanged(); };
            y += TrkH + 4;

            _lblSoftHigh    = AddLabel("임계값 High:", LblX, y + 5, LblW);
            _trkSoftHigh    = MakeTrackBar(TrkX, y, TrkW, TrkH, 0, 255, 200);
            _lblSoftHighVal = AddValueLabel(ValX, y + 5, ValW, "200");
            _trkSoftHigh.ValueChanged += (s, e) =>
            { _lblSoftHighVal.Text = _trkSoftHigh.Value.ToString(); OnThresholdChanged(); };
            y += TrkH + 4;

            AddLabel("최소 픽셀 수:", LblX, y + 4, LblW);
            _nudMinPixels = new NumericUpDown
            {
                Location = new Point(ComboX, y),
                Size     = new Size(ComboW, 21),
                Minimum  = 1,
                Maximum  = 999999,
                Value    = 1,
            };
            Controls.Add(_nudMinPixels);

            Size = new Size(362, y + 44);
            UpdateThresholdVisibility();
        }

        private TrackBar MakeTrackBar(int x, int y, int w, int h, int min, int max, int val)
        {
            var trk = new TrackBar
            {
                Location  = new Point(x, y),
                Size      = new Size(w, h),
                Minimum   = min,
                Maximum   = max,
                Value     = val,
                TickStyle = TickStyle.None,
                AutoSize  = false,
            };
            Controls.Add(trk);
            return trk;
        }

        private Label AddLabel(string text, int x, int y, int width)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 16), AutoSize = false };
            Controls.Add(lbl);
            return lbl;
        }

        private Label AddValueLabel(int x, int y, int width, string text)
        {
            var lbl = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 16), AutoSize = false, TextAlign = ContentAlignment.MiddleRight };
            Controls.Add(lbl);
            return lbl;
        }

        private void UpdateThresholdVisibility()
        {
            int idx = _cmbMode.SelectedIndex;
            var mode = (idx >= 0 && idx < Modes.Length)
                ? Modes[idx] : CogBlobSegmentationModeConstants.HardFixedThreshold;
            bool isHard = mode == CogBlobSegmentationModeConstants.HardFixedThreshold;
            bool isSoft = mode == CogBlobSegmentationModeConstants.SoftFixedThreshold;
            _lblHardThresh.Visible = isHard; _trkHardThresh.Visible = isHard; _lblHardVal.Visible = isHard;
            _lblSoftLow.Visible    = isSoft; _trkSoftLow.Visible    = isSoft; _lblSoftLowVal.Visible = isSoft;
            _lblSoftHigh.Visible   = isSoft; _trkSoftHigh.Visible   = isSoft; _lblSoftHighVal.Visible = isSoft;
        }

        private void OnThresholdChanged()
        {
            if (_binding || _boundStep == null) return;
            FlushStep(_boundStep);
            PreviewRequested?.Invoke(this, EventArgs.Empty);
        }

        // ── IStepParamPanel ──────────────────────────────────────────────

        public void BindStep(IVisionStep step)
        {
            _boundStep = step as CogBlobStep;
            if (_boundStep == null) return;
            _binding = true;
            try
            {
                var seg = _boundStep.RunParams.SegmentationParams;
                int modeIdx = Array.IndexOf(Modes, seg.Mode);
                _cmbMode.SelectedIndex = modeIdx >= 0 ? modeIdx : 0;
                int polIdx = Array.IndexOf(Polarities, seg.Polarity);
                _cmbPolarity.SelectedIndex = polIdx >= 0 ? polIdx : 0;
                _trkHardThresh.Value  = Clamp((int)seg.HardFixedThreshold,    0, 255);
                _lblHardVal.Text      = _trkHardThresh.Value.ToString();
                _trkSoftLow.Value     = Clamp((int)seg.SoftFixedThresholdLow,  0, 255);
                _lblSoftLowVal.Text   = _trkSoftLow.Value.ToString();
                _trkSoftHigh.Value    = Clamp((int)seg.SoftFixedThresholdHigh, 0, 255);
                _lblSoftHighVal.Text  = _trkSoftHigh.Value.ToString();
                _nudMinPixels.Value   = Clamp(_boundStep.RunParams.ConnectivityMinPixels, 1, 999999);
                UpdateThresholdVisibility();
            }
            finally { _binding = false; }
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogBlobStep;
            if (s == null) return;
            var seg = s.RunParams.SegmentationParams;
            int modeIdx = _cmbMode.SelectedIndex;
            if (modeIdx >= 0 && modeIdx < Modes.Length) seg.Mode = Modes[modeIdx];
            int polIdx = _cmbPolarity.SelectedIndex;
            if (polIdx >= 0 && polIdx < Polarities.Length) seg.Polarity = Polarities[polIdx];
            seg.HardFixedThreshold     = _trkHardThresh.Value;
            seg.SoftFixedThresholdLow  = _trkSoftLow.Value;
            seg.SoftFixedThresholdHigh = _trkSoftHigh.Value;
            s.RunParams.ConnectivityMinPixels = (int)_nudMinPixels.Value;
        }

        private static int Clamp(int v, int min, int max)     => v < min ? min : v > max ? max : v;

        private static decimal Clamp(decimal v, int min, int max) => v < min ? min : v > max ? max : v;
    }
}
