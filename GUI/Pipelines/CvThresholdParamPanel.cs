using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.OpenCV;
using OpenCvSharp;

namespace Vision.UI
{
    /// <summary>
    /// CvThresholdStep의 파라미터를 편집하는 UserControl.
    /// </summary>
    public class CvThresholdParamPanel : UserControl, IStepParamPanel, IInputImageSelectable
    {
        private NumericUpDown _nudThreshold;
        private NumericUpDown _nudMaxValue;
        private ComboBox      _cmbType;
        private ComboBox      _cmbInputImage;

        private readonly List<ImageSourceEntry> _inputImages = new List<ImageSourceEntry>();

        private static readonly ThresholdTypes[] Types =
        {
            ThresholdTypes.Binary, ThresholdTypes.BinaryInv, ThresholdTypes.Trunc,
            ThresholdTypes.Tozero, ThresholdTypes.TozeroInv, ThresholdTypes.Otsu,
        };
        private static readonly string[] TypeNames =
        {
            "Binary (이진화)", "BinaryInv (반전 이진화)", "Trunc (상한 클리핑)",
            "Tozero (하한 제로)", "TozeroInv (반전 하한 제로)", "Otsu (자동 임계값)",
        };

        public CvThresholdParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX = 8, LblW = 130, CtrlX = 145, CtrlW = 180, RowH = 32;
            int y = 8;

            AddLabel("임계값:", LblX, y + 3, LblW);
            _nudThreshold = new NumericUpDown { Location = new System.Drawing.Point(CtrlX, y), Size = new System.Drawing.Size(CtrlW, 21), DecimalPlaces = 1, Minimum = 0, Maximum = 255, Value = 128 };
            Controls.Add(_nudThreshold); y += RowH;

            AddLabel("최대값 (MaxValue):", LblX, y + 3, LblW);
            _nudMaxValue = new NumericUpDown { Location = new System.Drawing.Point(CtrlX, y), Size = new System.Drawing.Size(CtrlW, 21), DecimalPlaces = 1, Minimum = 0, Maximum = 255, Value = 255 };
            Controls.Add(_nudMaxValue); y += RowH;

            AddLabel("이진화 타입:", LblX, y + 3, LblW);
            _cmbType = new ComboBox { Location = new System.Drawing.Point(CtrlX, y), Size = new System.Drawing.Size(CtrlW, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbType.Items.AddRange(TypeNames);
            Controls.Add(_cmbType); y += RowH;

            AddLabel("입력 이미지:", LblX, y + 3, LblW);
            _cmbInputImage = new ComboBox { Location = new System.Drawing.Point(CtrlX, y), Size = new System.Drawing.Size(CtrlW + 40, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_cmbInputImage);

            Size = new System.Drawing.Size(370, y + 50);
        }

        private void AddLabel(string text, int x, int y, int width)
            => Controls.Add(new Label { Text = text, Location = new System.Drawing.Point(x, y), Size = new System.Drawing.Size(width, 16), AutoSize = false });

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
            var s = step as CvThresholdStep;
            if (s == null) return;
            _nudThreshold.Value = Clamp((decimal)s.ThresholdValue, 0, 255);
            _nudMaxValue.Value  = Clamp((decimal)s.MaxValue,       0, 255);
            int ti = Array.IndexOf(Types, s.Type);
            _cmbType.SelectedIndex = ti >= 0 ? ti : 0;

            _cmbInputImage.SelectedIndex = -1;
            for (int i = 0; i < _inputImages.Count; i++)
            {
                if (_inputImages[i].Key == s.InputImageKey)
                { _cmbInputImage.SelectedIndex = i; break; }
            }
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CvThresholdStep;
            if (s == null) return;
            s.ThresholdValue = (double)_nudThreshold.Value;
            s.MaxValue       = (double)_nudMaxValue.Value;
            int ti = _cmbType.SelectedIndex;
            if (ti >= 0 && ti < Types.Length) s.Type = Types[ti];
            int ii = _cmbInputImage.SelectedIndex;
            s.InputImageKey = (ii >= 0 && ii < _inputImages.Count) ? _inputImages[ii].Key : null;
        }

        private static decimal Clamp(decimal v, decimal min, decimal max)
            => v < min ? min : v > max ? max : v;
    }
}
