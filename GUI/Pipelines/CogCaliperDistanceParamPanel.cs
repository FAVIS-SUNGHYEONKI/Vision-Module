using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogCaliperDistanceStep의 파라미터 편집 패널.
    /// 두 Caliper 결과의 인덱스와 에지 인덱스를 설정합니다.
    /// </summary>
    public class CogCaliperDistanceParamPanel : UserControl, IStepParamPanel
    {
        private NumericUpDown _nudCaliperId_A;
        private NumericUpDown _nudEdgeIndex_A;
        private NumericUpDown _nudCaliperId_B;
        private NumericUpDown _nudEdgeIndex_B;

        public CogCaliperDistanceParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX = 8, LblW = 170, CtrlX = 182, CtrlW = 80, RowH = 32;
            int y = 8;

            AddSection("── Caliper A ──", LblX, y, LblW + CtrlW); y += 24;
            AddLabel("Caliper 인덱스 A:", LblX, y + 3, LblW);
            _nudCaliperId_A = MakeNud(CtrlX, y, CtrlW, 0, 99, 0); y += RowH;
            AddLabel("에지 인덱스 A:", LblX, y + 3, LblW);
            _nudEdgeIndex_A = MakeNud(CtrlX, y, CtrlW, 0, 99, 0); y += RowH;

            AddSection("── Caliper B ──", LblX, y, LblW + CtrlW); y += 24;
            AddLabel("Caliper 인덱스 B:", LblX, y + 3, LblW);
            _nudCaliperId_B = MakeNud(CtrlX, y, CtrlW, 0, 99, 1); y += RowH;
            AddLabel("에지 인덱스 B:", LblX, y + 3, LblW);
            _nudEdgeIndex_B = MakeNud(CtrlX, y, CtrlW, 0, 99, 0); y += RowH;

            y += 4;
            Controls.Add(new Label
            {
                Text      = "※ Caliper 인덱스: 파이프라인 실행 순서 기준\n   (첫 번째 Caliper=0, 두 번째=1, ...)\n※ 에지 인덱스: 각 Caliper 결과 내 에지 순서",
                Location  = new Point(LblX, y),
                Size      = new Size(LblW + CtrlW + 20, 55),
                AutoSize  = false,
                ForeColor = Color.DimGray,
                Font      = new System.Drawing.Font(Font.FontFamily, 7.5f),
            });
            y += 60;
            Size = new Size(280, y);
        }

        private NumericUpDown MakeNud(int x, int y, int w, int min, int max, int val)
        {
            var nud = new NumericUpDown { Location = new Point(x, y), Size = new Size(w, 21), Minimum = min, Maximum = max, Value = val };
            Controls.Add(nud);
            return nud;
        }

        private void AddLabel(string text, int x, int y, int width)
            => Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 16), AutoSize = false });

        private void AddSection(string text, int x, int y, int width)
            => Controls.Add(new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 16), AutoSize = false, ForeColor = Color.DarkBlue, Font = new System.Drawing.Font(Font.FontFamily, 8f, System.Drawing.FontStyle.Bold) });

        public void BindStep(IVisionStep step)
        {
            var s = step as CogCaliperDistanceStep;
            if (s == null) return;
            _nudCaliperId_A.Value = Clamp(s.CaliperId_A, 0, 99);
            _nudEdgeIndex_A.Value = Clamp(s.EdgeIndex_A, 0, 99);
            _nudCaliperId_B.Value = Clamp(s.CaliperId_B, 0, 99);
            _nudEdgeIndex_B.Value = Clamp(s.EdgeIndex_B, 0, 99);
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogCaliperDistanceStep;
            if (s == null) return;
            s.CaliperId_A = (int)_nudCaliperId_A.Value;
            s.EdgeIndex_A = (int)_nudEdgeIndex_A.Value;
            s.CaliperId_B = (int)_nudCaliperId_B.Value;
            s.EdgeIndex_B = (int)_nudEdgeIndex_B.Value;
        }

        private static decimal Clamp(int v, int min, int max)
            => v < min ? min : v > max ? max : v;
    }
}
