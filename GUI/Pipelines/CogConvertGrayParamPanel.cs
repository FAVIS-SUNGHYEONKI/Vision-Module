using System;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogConvertGray 스텝의 채널(R/G/B) 선택 파라미터 패널.
    /// </summary>
    public class CogConvertGrayParamPanel : UserControl, IStepParamPanel
    {
        private ComboBox _cmbChannel;

        private static readonly CogConvertGray.ColorChannel[] Channels =
        {
            CogConvertGray.ColorChannel.Red,
            CogConvertGray.ColorChannel.Green,
            CogConvertGray.ColorChannel.Blue,
        };
        private static readonly string[] ChannelNames =
        {
            "Plane 0 — Red (R)",
            "Plane 1 — Green (G)",
            "Plane 2 — Blue (B)",
        };

        public CogConvertGrayParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            Controls.Add(new Label
            {
                Text     = "추출 채널 (Plane):",
                Location = new Point(8, 11),
                Size     = new Size(150, 16),
                AutoSize = false,
            });
            _cmbChannel = new ComboBox
            {
                Location      = new Point(165, 8),
                Size          = new Size(170, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbChannel.Items.AddRange(ChannelNames);
            Controls.Add(_cmbChannel);
            Size = new Size(360, 48);
        }

        public void BindStep(IVisionStep step)
        {
            var s = step as CogConvertGray;
            if (s == null) return;
            int idx = Array.IndexOf(Channels, s.Channel);
            _cmbChannel.SelectedIndex = idx >= 0 ? idx : 1;
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogConvertGray;
            if (s == null) return;
            int idx = _cmbChannel.SelectedIndex;
            if (idx >= 0 && idx < Channels.Length)
                s.Channel = Channels[idx];
        }
    }
}
