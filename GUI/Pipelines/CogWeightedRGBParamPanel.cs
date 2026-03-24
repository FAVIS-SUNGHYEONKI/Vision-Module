using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogWeightedRGBStep의 R/G/B 가중치 파라미터 패널.
    /// 프리셋 콤보박스와 개별 TrackBar/NumericUpDown으로 가중치를 조절한다.
    /// </summary>
    public class CogWeightedRGBParamPanel : UserControl, IStepParamPanel, IInputImageSelectable
    {
        /// <summary>가중치/프리셋 변경 시 발생 — PipelineEditorForm이 구독하여 실시간 미리보기를 실행합니다.</summary>
        public event EventHandler PreviewRequested;

        private ComboBox        _cmbPreset;
        private NumericUpDown   _nudRed;
        private NumericUpDown   _nudGreen;
        private NumericUpDown   _nudBlue;
        private ComboBox        _cmbInputImage;
        private bool            _syncing;
        private IVisionStep     _boundStep;

        private readonly List<ImageSourceEntry> _inputImages = new List<ImageSourceEntry>();

        private static readonly (string Label, double R, double G, double B)[] Presets =
        {
            ("균등 (1/3, 1/3, 1/3)",         1.0/3.0, 1.0/3.0, 1.0/3.0),
            ("BT.601 휘도 (0.299, 0.587, 0.114)", 0.299,   0.587,   0.114),
            ("Red 채널만 (1, 0, 0)",           1.0,     0.0,     0.0),
            ("Green 채널만 (0, 1, 0)",         0.0,     1.0,     0.0),
            ("Blue 채널만 (0, 0, 1)",          0.0,     0.0,     1.0),
            ("사용자 정의",                    -1,      -1,      -1),
        };

        public CogWeightedRGBParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            int lw = 60, nw = 80, gap = 8, x0 = 8, y = 8;

            Controls.Add(new Label { Text = "프리셋:", Location = new Point(x0, y + 3), AutoSize = true });
            _cmbPreset = new ComboBox
            {
                Location      = new Point(x0 + 50, y),
                Size          = new Size(260, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            foreach (var p in Presets) _cmbPreset.Items.Add(p.Label);
            _cmbPreset.SelectedIndexChanged += OnPresetChanged;
            Controls.Add(_cmbPreset);

            y += 32;
            _nudRed   = AddWeightRow("Red",   x0, y, lw, nw);  y += 28;
            _nudGreen = AddWeightRow("Green", x0, y, lw, nw);  y += 28;
            _nudBlue  = AddWeightRow("Blue",  x0, y, lw, nw);  y += 28;

            Controls.Add(new Label { Text = "입력 이미지:", Location = new Point(x0, y + 3), Size = new Size(80, 16) });
            _cmbInputImage = new ComboBox { Location = new Point(x0 + 84, y), Size = new Size(230, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_cmbInputImage);
            y += 32;

            Size = new Size(340, y);
        }

        private NumericUpDown AddWeightRow(string label, int x, int y, int lw, int nw)
        {
            Controls.Add(new Label { Text = label + ":", Location = new Point(x, y + 3), Size = new Size(lw, 16) });
            var nud = new NumericUpDown
            {
                Location      = new Point(x + lw + 4, y),
                Size          = new Size(nw, 21),
                DecimalPlaces = 3,
                Minimum       = 0,
                Maximum       = 10,
                Increment     = 0.05m,
            };
            nud.ValueChanged += OnWeightChanged;
            Controls.Add(nud);
            return nud;
        }

        private void OnPresetChanged(object sender, EventArgs e)
        {
            if (_syncing) return;
            int idx = _cmbPreset.SelectedIndex;
            if (idx < 0 || idx >= Presets.Length) return;
            var p = Presets[idx];
            if (p.R < 0) return;   // 사용자 정의: 값 변경 없음

            _syncing = true;
            _nudRed.Value   = (decimal)p.R;
            _nudGreen.Value = (decimal)p.G;
            _nudBlue.Value  = (decimal)p.B;
            _syncing = false;

            FirePreview();
        }

        private void OnWeightChanged(object sender, EventArgs e)
        {
            if (_syncing) return;

            double r = (double)_nudRed.Value;
            double g = (double)_nudGreen.Value;
            double b = (double)_nudBlue.Value;

            _syncing = true;
            int match = Array.FindIndex(Presets, p =>
                p.R >= 0 && Approx(p.R, r) && Approx(p.G, g) && Approx(p.B, b));
            _cmbPreset.SelectedIndex = match >= 0 ? match : Presets.Length - 1;
            _syncing = false;

            FirePreview();
        }

        private void FirePreview()
        {
            if (_boundStep == null) return;
            FlushStep(_boundStep);
            PreviewRequested?.Invoke(this, EventArgs.Empty);
        }

        private static bool Approx(double a, double b) => Math.Abs(a - b) < 0.0005;

        // ── IInputImageSelectable ─────────────────────────────────────────

        public void SetAvailableInputImages(IReadOnlyList<ImageSourceEntry> images)
        {
            _inputImages.Clear();
            _inputImages.AddRange(images);
            _cmbInputImage.Items.Clear();
            foreach (var e in _inputImages) _cmbInputImage.Items.Add(e);
        }

        // ── IStepParamPanel ───────────────────────────────────────────────

        public void BindStep(IVisionStep step)
        {
            var s = step as CogWeightedRGBStep;
            if (s == null) return;
            _boundStep = step;

            _syncing = true;
            _nudRed.Value   = (decimal)Math.Round(s.RedWeight,   3);
            _nudGreen.Value = (decimal)Math.Round(s.GreenWeight, 3);
            _nudBlue.Value  = (decimal)Math.Round(s.BlueWeight,  3);
            _syncing = false;

            // 프리셋 표시 동기화 (미리보기 없이)
            double r = (double)_nudRed.Value, g = (double)_nudGreen.Value, b = (double)_nudBlue.Value;
            _syncing = true;
            int match = Array.FindIndex(Presets, p =>
                p.R >= 0 && Approx(p.R, r) && Approx(p.G, g) && Approx(p.B, b));
            _cmbPreset.SelectedIndex = match >= 0 ? match : Presets.Length - 1;
            _syncing = false;

            _cmbInputImage.SelectedIndex = -1;
            for (int i = 0; i < _inputImages.Count; i++)
            {
                if (_inputImages[i].Key == s.InputImageKey)
                { _cmbInputImage.SelectedIndex = i; break; }
            }
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogWeightedRGBStep;
            if (s == null) return;
            s.RedWeight   = (double)_nudRed.Value;
            s.GreenWeight = (double)_nudGreen.Value;
            s.BlueWeight  = (double)_nudBlue.Value;
            int ii = _cmbInputImage.SelectedIndex;
            s.InputImageKey = (ii >= 0 && ii < _inputImages.Count) ? _inputImages[ii].Key : null;
        }
    }
}
