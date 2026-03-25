using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogConvertGrey 스텝의 채널(R/G/B) 선택 파라미터 패널.
    /// </summary>
    public class CogConvertGreyParamPanel : UserControl, IStepParamPanel, IInputImageSelectable
    {
        private ComboBox _cmbChannel;
        private ComboBox _cmbInputImage;

        private readonly List<ImageSourceEntry> _inputImages = new List<ImageSourceEntry>();
        public event EventHandler<string> InputImageKeyChanged;

        private static readonly ColorChannel[] Channels =
        {
            ColorChannel.Red,
            ColorChannel.Green,
            ColorChannel.Blue,
        };
        private static readonly string[] ChannelNames =
        {
            "Plane 0 — Red (R)",
            "Plane 1 — Green (G)",
            "Plane 2 — Blue (B)",
        };

        public CogConvertGreyParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX = 8, LblW = 150, CtrlX = 165, CtrlW = 170, RowH = 32;
            int y = 8;

            Controls.Add(new Label { Text = "추출 채널 (Plane):", Location = new Point(LblX, y + 3), Size = new Size(LblW, 16), AutoSize = false });
            _cmbChannel = new ComboBox { Location = new Point(CtrlX, y), Size = new Size(CtrlW, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbChannel.Items.AddRange(ChannelNames);
            Controls.Add(_cmbChannel);
            y += RowH;

            Controls.Add(new Label { Text = "입력 이미지:", Location = new Point(LblX, y + 3), Size = new Size(LblW, 16), AutoSize = false });
            _cmbInputImage = new ComboBox { Location = new Point(CtrlX, y), Size = new Size(CtrlW + 40, 21), DropDownStyle = ComboBoxStyle.DropDownList };
            Controls.Add(_cmbInputImage);
            _cmbInputImage.SelectedIndexChanged += (s, e) =>
            {
                var idx = _cmbInputImage.SelectedIndex;
                InputImageKeyChanged?.Invoke(this,
                    idx >= 0 && idx < _inputImages.Count ? _inputImages[idx].Key : null);
            };

            Size = new Size(380, y + 50);
        }

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
            var s = step as CogConvertGrey;
            if (s == null) return;
            int idx = Array.IndexOf(Channels, s.Channel);
            _cmbChannel.SelectedIndex = idx >= 0 ? idx : 1;

            _cmbInputImage.SelectedIndex = -1;
            for (int i = 0; i < _inputImages.Count; i++)
            {
                if (_inputImages[i].Key == s.InputImageKey)
                { _cmbInputImage.SelectedIndex = i; break; }
            }
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogConvertGrey;
            if (s == null) return;
            int idx = _cmbChannel.SelectedIndex;
            if (idx >= 0 && idx < Channels.Length)
                s.Channel = Channels[idx];
            int ii = _cmbInputImage.SelectedIndex;
            s.InputImageKey = (ii >= 0 && ii < _inputImages.Count) ? _inputImages[ii].Key : null;
        }
    }
}
