using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogFixtureStep의 파라미터를 편집하는 UserControl.
    /// </summary>
    public class CogFixtureParamPanel : UserControl, IStepParamPanel, IInputImageSelectable
    {
        private NumericUpDown _nudTransX;
        private NumericUpDown _nudTransY;
        private NumericUpDown _nudRotDeg;
        private NumericUpDown _nudScaling;
        private TextBox       _txtSourceKey;
        private ComboBox      _cmbInputImage;
        private Label         _lblNote;

        private readonly List<ImageSourceEntry> _inputImages = new List<ImageSourceEntry>();

        public CogFixtureParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX  = 8;
            const int LblW  = 145;
            const int CtrlX = 158;
            const int CtrlW = 185;
            const int RowH  = 32;
            int y = 8;

            // ── 런타임 소스 키 ────────────────────────────────────────────
            AddLabel("변환 소스 키:", LblX, y + 3, LblW);
            _txtSourceKey = new TextBox
            {
                Location    = new Point(CtrlX, y),
                Size        = new Size(CtrlW + 50, 21),
                PlaceholderText = "예: VisionPro.PMAlign.0.Pose",
            };
            Controls.Add(_txtSourceKey);
            y += RowH;

            _lblNote = new Label
            {
                Text      = "* 소스 키가 설정되면 아래 수동 값은 무시됩니다.",
                Location  = new Point(LblX, y),
                Size      = new Size(380, 16),
                ForeColor = Color.DimGray,
                Font      = new Font(Font.FontFamily, 7.5f),
                AutoSize  = false,
            };
            Controls.Add(_lblNote);
            y += 22;

            // ── 구분선 ────────────────────────────────────────────────────
            var sep = new Label
            {
                BorderStyle = BorderStyle.Fixed3D,
                Location    = new Point(LblX, y),
                Size        = new Size(CtrlX + CtrlW + 50 - LblX, 2),
                AutoSize    = false,
            };
            Controls.Add(sep);
            y += 10;

            // ── 수동 변환 파라미터 ────────────────────────────────────────
            AddLabel("이동 X (px):", LblX, y + 3, LblW);
            _nudTransX = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DecimalPlaces = 2,
                Minimum       = -99999,
                Maximum       =  99999,
                Increment     = 1,
            };
            Controls.Add(_nudTransX);
            y += RowH;

            AddLabel("이동 Y (px):", LblX, y + 3, LblW);
            _nudTransY = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DecimalPlaces = 2,
                Minimum       = -99999,
                Maximum       =  99999,
                Increment     = 1,
            };
            Controls.Add(_nudTransY);
            y += RowH;

            AddLabel("회전 (°):", LblX, y + 3, LblW);
            _nudRotDeg = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DecimalPlaces = 3,
                Minimum       = -360,
                Maximum       =  360,
                Increment     = 0.1m,
            };
            Controls.Add(_nudRotDeg);
            y += RowH;

            AddLabel("배율:", LblX, y + 3, LblW);
            _nudScaling = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DecimalPlaces = 4,
                Minimum       = (decimal)0.0001,
                Maximum       = 100,
                Increment     = (decimal)0.01,
            };
            Controls.Add(_nudScaling);
            y += RowH;

            // ── 입력 이미지 ───────────────────────────────────────────────
            AddLabel("입력 이미지:", LblX, y + 3, LblW);
            _cmbInputImage = new ComboBox
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW + 50, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            Controls.Add(_cmbInputImage);

            Size = new Size(CtrlX + CtrlW + 65, y + 40);
        }

        private void AddLabel(string text, int x, int y, int width)
            => Controls.Add(new Label
            {
                Text     = text,
                Location = new Point(x, y),
                Size     = new Size(width, 16),
                AutoSize = false,
            });

        // ── IInputImageSelectable ────────────────────────────────────────

        public void SetAvailableInputImages(IReadOnlyList<ImageSourceEntry> images)
        {
            _inputImages.Clear();
            _inputImages.AddRange(images);
            _cmbInputImage.Items.Clear();
            foreach (var e in _inputImages) _cmbInputImage.Items.Add(e);
        }

        // ── IStepParamPanel ──────────────────────────────────────────────

        public void BindStep(IVisionStep step)
        {
            var s = step as CogFixtureStep;
            if (s == null) return;

            _txtSourceKey.Text = s.TransformSourceKey ?? string.Empty;
            _nudTransX.Value   = Clamp((decimal)s.TranslationX, _nudTransX.Minimum, _nudTransX.Maximum);
            _nudTransY.Value   = Clamp((decimal)s.TranslationY, _nudTransY.Minimum, _nudTransY.Maximum);
            _nudRotDeg.Value   = Clamp((decimal)s.RotationDeg,  _nudRotDeg.Minimum,  _nudRotDeg.Maximum);
            _nudScaling.Value  = Clamp((decimal)s.Scaling,      _nudScaling.Minimum, _nudScaling.Maximum);

            _cmbInputImage.SelectedIndex = -1;
            for (int i = 0; i < _inputImages.Count; i++)
            {
                if (_inputImages[i].Key == s.InputImageKey)
                { _cmbInputImage.SelectedIndex = i; break; }
            }

            UpdateManualEnabled(s.TransformSourceKey);
            _txtSourceKey.TextChanged += (_, __) =>
                UpdateManualEnabled(_txtSourceKey.Text);
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogFixtureStep;
            if (s == null) return;

            s.TransformSourceKey = string.IsNullOrWhiteSpace(_txtSourceKey.Text)
                ? null : _txtSourceKey.Text.Trim();
            s.TranslationX = (double)_nudTransX.Value;
            s.TranslationY = (double)_nudTransY.Value;
            s.RotationDeg  = (double)_nudRotDeg.Value;
            s.Scaling      = (double)_nudScaling.Value;

            int ii = _cmbInputImage.SelectedIndex;
            s.InputImageKey = (ii >= 0 && ii < _inputImages.Count) ? _inputImages[ii].Key : null;
        }

        // ── 내부 헬퍼 ────────────────────────────────────────────────────

        /// <summary>소스 키가 설정된 경우 수동 파라미터 컨트롤을 비활성화한다.</summary>
        private void UpdateManualEnabled(string sourceKey)
        {
            bool manual = string.IsNullOrWhiteSpace(sourceKey);
            _nudTransX.Enabled = manual;
            _nudTransY.Enabled = manual;
            _nudRotDeg.Enabled = manual;
            _nudScaling.Enabled = manual;
        }

        private static decimal Clamp(decimal v, decimal min, decimal max)
            => v < min ? min : v > max ? max : v;
    }
}
