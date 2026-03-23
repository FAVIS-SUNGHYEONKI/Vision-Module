#if PMALIGN_ENABLED
using System;
using System.Drawing;
using System.Windows.Forms;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogPMAlignStep의 파라미터 편집 패널.
    ///
    /// 패턴 학습 상태 표시, 학습 버튼, AcceptThreshold, MaxResults,
    /// 각도 검색 범위(ZoneAngle), 스케일 검색 범위(ZoneScale)를 제공한다.
    ///
    /// 패턴 학습:
    ///   TrainRequested 이벤트를 발생시키면 PipelineEditorForm이
    ///   현재 이미지와 TrainRegion으로 실제 학습을 수행한 후
    ///   UpdateTrainStatus()를 호출하여 상태를 갱신한다.
    /// </summary>
    public class CogPMAlignParamPanel : UserControl, IStepParamPanel
    {
        /// <summary>
        /// "패턴 학습" 버튼 클릭 시 발생.
        /// PipelineEditorForm이 구독하여 현재 이미지로 학습을 실행한다.
        /// </summary>
        public event EventHandler TrainRequested;

        private Label         _lblTrainStatus;
        private Button        _btnTrain;
        private NumericUpDown _nudAcceptThreshold;
        private NumericUpDown _nudMaxResults;
        private NumericUpDown _nudZoneAngleLow;
        private NumericUpDown _nudZoneAngleHigh;
        private NumericUpDown _nudZoneScaleLow;
        private NumericUpDown _nudZoneScaleHigh;

        private bool _syncing;

        public CogPMAlignParamPanel() { BuildUI(); }

        private void BuildUI()
        {
            const int LblX  = 8;
            const int LblW  = 160;
            const int CtrlX = 172;
            const int CtrlW = 120;
            const int RowH  = 30;
            int y = 8;

            // ── 패턴 학습 상태 ────────────────────────────────────────────
            var grpTrain = new GroupBox
            {
                Text     = "패턴 학습",
                Location = new Point(LblX, y),
                Size     = new Size(300, 70),
            };

            _lblTrainStatus = new Label
            {
                Text      = "패턴 미학습",
                Location  = new Point(10, 20),
                Size      = new Size(170, 20),
                ForeColor = Color.OrangeRed,
                Font      = new Font(Font.FontFamily, 9f, FontStyle.Bold),
            };
            grpTrain.Controls.Add(_lblTrainStatus);

            _btnTrain = new Button
            {
                Text     = "패턴 학습",
                Location = new Point(188, 16),
                Size     = new Size(96, 28),
            };
            _btnTrain.Click += (s, e) => TrainRequested?.Invoke(this, EventArgs.Empty);
            grpTrain.Controls.Add(_btnTrain);

            Controls.Add(grpTrain);
            y += 80;

            // ── 런타임 파라미터 ───────────────────────────────────────────
            AddLabel("허용 임계값 (0~1):", LblX, y + 3, LblW);
            _nudAcceptThreshold = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(CtrlW, 21),
                DecimalPlaces = 2,
                Minimum       = 0,
                Maximum       = 1,
                Increment     = 0.05m,
            };
            Controls.Add(_nudAcceptThreshold);
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

            // ── 각도 검색 범위 ────────────────────────────────────────────
            var lblAngle = new Label
            {
                Text     = "각도 범위 (°):",
                Location = new Point(LblX, y + 3),
                Size     = new Size(LblW, 16),
            };
            Controls.Add(lblAngle);

            _nudZoneAngleLow = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(55, 21),
                DecimalPlaces = 0,
                Minimum       = -180,
                Maximum       = 0,
                Increment     = 5,
            };
            Controls.Add(_nudZoneAngleLow);

            Controls.Add(new Label
            {
                Text     = "~",
                Location = new Point(CtrlX + 60, y + 3),
                Size     = new Size(12, 16),
            });

            _nudZoneAngleHigh = new NumericUpDown
            {
                Location      = new Point(CtrlX + 75, y),
                Size          = new Size(55, 21),
                DecimalPlaces = 0,
                Minimum       = 0,
                Maximum       = 180,
                Increment     = 5,
            };
            Controls.Add(_nudZoneAngleHigh);
            y += RowH;

            // ── 스케일 검색 범위 ──────────────────────────────────────────
            Controls.Add(new Label
            {
                Text     = "스케일 범위:",
                Location = new Point(LblX, y + 3),
                Size     = new Size(LblW, 16),
            });

            _nudZoneScaleLow = new NumericUpDown
            {
                Location      = new Point(CtrlX, y),
                Size          = new Size(55, 21),
                DecimalPlaces = 2,
                Minimum       = 0.1m,
                Maximum       = 2.0m,
                Increment     = 0.05m,
            };
            Controls.Add(_nudZoneScaleLow);

            Controls.Add(new Label
            {
                Text     = "~",
                Location = new Point(CtrlX + 60, y + 3),
                Size     = new Size(12, 16),
            });

            _nudZoneScaleHigh = new NumericUpDown
            {
                Location      = new Point(CtrlX + 75, y),
                Size          = new Size(55, 21),
                DecimalPlaces = 2,
                Minimum       = 0.1m,
                Maximum       = 2.0m,
                Increment     = 0.05m,
            };
            Controls.Add(_nudZoneScaleHigh);
            y += RowH + 8;

            Size = new Size(310, y);
        }

        private void AddLabel(string text, int x, int y, int width)
            => Controls.Add(new Label
            {
                Text     = text,
                Location = new Point(x, y),
                Size     = new Size(width, 16),
                AutoSize = false,
            });

        // ── 학습 상태 업데이트 ───────────────────────────────────────────

        /// <summary>
        /// 학습 상태 레이블을 업데이트한다.
        /// PipelineEditorForm이 TrainRequested 처리 후 호출한다.
        /// </summary>
        public void UpdateTrainStatus(bool isTrained)
        {
            if (InvokeRequired) { Invoke(new Action(() => UpdateTrainStatus(isTrained))); return; }
            _lblTrainStatus.Text      = isTrained ? "패턴 학습됨 ✓" : "패턴 미학습";
            _lblTrainStatus.ForeColor = isTrained ? Color.Green : Color.OrangeRed;
        }

        // ── IStepParamPanel ──────────────────────────────────────────────

        public void BindStep(IVisionStep step)
        {
            var s = step as CogPMAlignStep;
            if (s == null) return;
            _syncing = true;
            try
            {
                UpdateTrainStatus(s.IsPatternTrained);

                _nudAcceptThreshold.Value = Clamp((decimal)s.RunParams.AcceptThreshold, 0, 1);
                _nudMaxResults.Value      = Clamp(s.MaxResults, 1, 20);

                double lowDeg  = s.RunParams.ZoneAngle.Low  * 180 / Math.PI;
                double highDeg = s.RunParams.ZoneAngle.High * 180 / Math.PI;
                _nudZoneAngleLow.Value  = Clamp((decimal)lowDeg,  -180, 0);
                _nudZoneAngleHigh.Value = Clamp((decimal)highDeg,    0, 180);

                _nudZoneScaleLow.Value  = Clamp((decimal)s.RunParams.ZoneScale.Low,  0.1m, 2.0m);
                _nudZoneScaleHigh.Value = Clamp((decimal)s.RunParams.ZoneScale.High, 0.1m, 2.0m);
            }
            finally { _syncing = false; }
        }

        public void FlushStep(IVisionStep step)
        {
            var s = step as CogPMAlignStep;
            if (s == null) return;

            s.RunParams.AcceptThreshold = (double)_nudAcceptThreshold.Value;
            s.MaxResults                = (int)_nudMaxResults.Value;

            s.RunParams.ZoneAngle.Low  = (double)_nudZoneAngleLow.Value  * Math.PI / 180;
            s.RunParams.ZoneAngle.High = (double)_nudZoneAngleHigh.Value * Math.PI / 180;

            s.RunParams.ZoneScale.Low  = (double)_nudZoneScaleLow.Value;
            s.RunParams.ZoneScale.High = (double)_nudZoneScaleHigh.Value;
        }

        private static decimal Clamp(decimal v, decimal min, decimal max)
            => v < min ? min : v > max ? max : v;
        private static decimal Clamp(int v, int min, int max)
            => v < min ? min : v > max ? max : v;
    }
}
#endif // PMALIGN_ENABLED
