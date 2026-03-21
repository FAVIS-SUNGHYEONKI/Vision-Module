using System;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;
using Cognex.VisionPro.Caliper;

namespace Vision.UI
{
    /// <summary>
    /// CogCaliperStep의 파라미터를 편집하는 UserControl.
    /// PipelineEditorForm 또는 외부 폼에 직접 임베드할 수 있습니다.
    /// </summary>
    public class CogCaliperParamPanel : UserControl, IStepParamPanel
    {
        private NumericUpDown _nudContrast;
        private ComboBox      _cmbEdgeMode;
        private ComboBox      _cmbPolarity;
        private NumericUpDown _nudFilterSize;
        private NumericUpDown _nudMaxResults;
        private ComboBox      _cmbSelectionMode;

        private bool _syncing;

        private static readonly CogCaliperEdgeModeConstants[] EdgeModes =
        {
            CogCaliperEdgeModeConstants.SingleEdge,
        };
        private static readonly string[] EdgeModeNames =
        {
            "Single Edge (단일 에지)",
        };

        private static readonly CaliperSelectionMode[] SelectionModes =
        {
            CaliperSelectionMode.FirstEdge,
            CaliperSelectionMode.BestEdge,
        };
        private static readonly string[] SelectionModeNames =
        {
            "First Edge (첫 번째 에지)",
            "Best Edge (최고 점수 에지)",
        };
        private static readonly CogCaliperPolarityConstants[] Polarities =
        {
            CogCaliperPolarityConstants.DontCare,
            CogCaliperPolarityConstants.DarkToLight,
            CogCaliperPolarityConstants.LightToDark,
        };
        private static readonly string[] PolarityNames =
        {
            "DontCare (무관)",
            "DarkToLight (어두움→밝음)",
            "LightToDark (밝음→어두움)",
        };

        public CogCaliperParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX  = 8;
            const int LblW  = 140;
            const int CtrlX = 155;
            const int CtrlW = 180;
            const int RowH  = 32;
            int y = 8;

            AddLabel("대비 임계값:", LblX, y + 3, LblW);
            _nudContrast = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DecimalPlaces = 1,
                Minimum       = 0,
                Maximum       = 255,
                Increment     = 0.5m,
            };
            Controls.Add(_nudContrast);
            y += RowH;

            AddLabel("에지 모드:", LblX, y + 3, LblW);
            _cmbEdgeMode = new ComboBox
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbEdgeMode.Items.AddRange(EdgeModeNames);
            Controls.Add(_cmbEdgeMode);
            y += RowH;

            AddLabel("에지 극성:", LblX, y + 3, LblW);
            _cmbPolarity = new ComboBox
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbPolarity.Items.AddRange(PolarityNames);
            Controls.Add(_cmbPolarity);
            y += RowH;

            AddLabel("필터 크기 (px):", LblX, y + 3, LblW);
            _nudFilterSize = new NumericUpDown
            {
                Location = new Point(CtrlX, y),
                Size     = new Size(80, 21),
                Minimum  = 1,
                Maximum  = 50,
            };
            Controls.Add(_nudFilterSize);
            y += RowH;

            AddLabel("최대 검출 수:", LblX, y + 3, LblW);
            _nudMaxResults = new NumericUpDown
            {
                Location = new Point(CtrlX, y),
                Size     = new Size(80, 21),
                Minimum  = 1,
                Maximum  = 20,
            };
            Controls.Add(_nudMaxResults);
            y += RowH;

            AddLabel("선택 모드:", LblX, y + 3, LblW);
            _cmbSelectionMode = new ComboBox
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            _cmbSelectionMode.Items.AddRange(SelectionModeNames);
            Controls.Add(_cmbSelectionMode);

            Size = new Size(350, y + 40);
        }

        private void AddLabel(string text, int x, int y, int width)
            => Controls.Add(new Label
            {
                Text     = text,
                Location = new Point(x, y),
                Size     = new Size(width, 16),
                AutoSize = false,
            });

        // ── IStepParamPanel ──────────────────────────────────────────────

        public void BindStep(IVisionStep step)
        {
            var s = step as CogCaliperStep;
            if (s == null) return;
            _syncing = true;
            try
            {
                _nudContrast.Value              = Clamp((decimal)s.RunParams.ContrastThreshold, _nudContrast.Minimum, _nudContrast.Maximum);
                _cmbEdgeMode.SelectedIndex      = Math.Max(0, Array.IndexOf(EdgeModes,      s.RunParams.EdgeMode));
                _cmbPolarity.SelectedIndex      = Math.Max(0, Array.IndexOf(Polarities,     s.RunParams.Edge0Polarity));
                _nudFilterSize.Value            = Clamp(s.RunParams.FilterHalfSizeInPixels, (int)_nudFilterSize.Minimum, (int)_nudFilterSize.Maximum);
                _nudMaxResults.Value            = Clamp(s.RunParams.MaxResults,             (int)_nudMaxResults.Minimum, (int)_nudMaxResults.Maximum);
                _cmbSelectionMode.SelectedIndex = Math.Max(0, Array.IndexOf(SelectionModes, s.SelectionMode));
            }
            finally { _syncing = false; }
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogCaliperStep;
            if (s == null) return;
            s.RunParams.ContrastThreshold = (double)_nudContrast.Value;
            int ei = _cmbEdgeMode.SelectedIndex;
            if (ei >= 0 && ei < EdgeModes.Length)      s.RunParams.EdgeMode      = EdgeModes[ei];
            int pi = _cmbPolarity.SelectedIndex;
            if (pi >= 0 && pi < Polarities.Length)     s.RunParams.Edge0Polarity = Polarities[pi];
            s.RunParams.FilterHalfSizeInPixels = (int)_nudFilterSize.Value;
            s.RunParams.MaxResults             = (int)_nudMaxResults.Value;
            int si = _cmbSelectionMode.SelectedIndex;
            if (si >= 0 && si < SelectionModes.Length) s.SelectionMode           = SelectionModes[si];
        }

        private static decimal Clamp(decimal v, decimal min, decimal max)
            => v < min ? min : v > max ? max : v;
        private static decimal Clamp(int v, int min, int max)
            => v < min ? min : v > max ? max : v;
    }
}
