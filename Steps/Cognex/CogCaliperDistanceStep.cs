using System;
using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// 파이프라인 내 두 CogCaliperStep의 에지 결과 간 거리를 측정하는 스텝.
    ///
    /// 동작:
    ///   1. VisionContext.Data["VisionPro.Caliper.{CaliperId_A}"] 에서 에지 결과 A를 읽습니다.
    ///   2. VisionContext.Data["VisionPro.Caliper.{CaliperId_B}"] 에서 에지 결과 B를 읽습니다.
    ///   3. 각 결과와 연관된 Region이 있으면 1D Position → 2D (X, Y)로 변환합니다.
    ///      Region이 없으면 Position을 X로, Y=0으로 사용합니다.
    ///   4. 두 점 사이의 유클리드 거리를 계산하여 CaliperDistanceResult에 저장합니다.
    ///
    /// 결과 키: "VisionPro.CaliperDistance.{N}" (여러 개 등록 시 N 자동 증가)
    /// 결과 타입: CaliperDistanceResult
    ///
    /// 전제 조건:
    ///   - 이 스텝이 실행되기 전에 참조된 CogCaliperStep들이 먼저 실행되어야 합니다.
    ///   - CaliperId_A/B 는 파이프라인 실행 순서대로 부여된 Caliper 키 인덱스입니다.
    ///     예: 파이프라인에 Caliper 스텝이 2개이면 CaliperId 0, 1
    ///
    /// XML 직렬화:
    ///   &lt;Step type="VisionPro.CaliperDistance"&gt;
    ///     &lt;CaliperId_A&gt;0&lt;/CaliperId_A&gt;
    ///     &lt;EdgeIndex_A&gt;0&lt;/EdgeIndex_A&gt;
    ///     &lt;CaliperId_B&gt;1&lt;/CaliperId_B&gt;
    ///     &lt;EdgeIndex_B&gt;0&lt;/EdgeIndex_B&gt;
    ///   &lt;/Step&gt;
    /// </summary>
    public class CogCaliperDistanceStep : IVisionStep, IImageTypedStep, IStepSerializable, IInspectionStep
    {
        /// <summary>스텝 고유 이름.</summary>
        public string Name => "VisionPro.CaliperDistance";

        /// <summary>이미지를 사용하지 않으므로 어떤 타입도 허용합니다.</summary>
        public bool ContinueOnFailure => false;

        /// <summary>이미지를 읽거나 변환하지 않으므로 Any를 반환합니다.</summary>
        public ImageType RequiredInputType  => ImageType.Any;

        /// <summary>이미지를 변환하지 않으므로 Any를 반환합니다.</summary>
        public ImageType ProducedOutputType => ImageType.Any;

        /// <summary>참조할 Caliper 결과 A의 인덱스 (0-based). 기본값 0.</summary>
        public int CaliperId_A  { get; set; } = 0;

        /// <summary>Caliper A 결과 내에서 사용할 에지 인덱스 (0-based). 기본값 0.</summary>
        public int EdgeIndex_A  { get; set; } = 0;

        /// <summary>참조할 Caliper 결과 B의 인덱스 (0-based). 기본값 1.</summary>
        public int CaliperId_B  { get; set; } = 1;

        /// <summary>Caliper B 결과 내에서 사용할 에지 인덱스 (0-based). 기본값 0.</summary>
        public int EdgeIndex_B  { get; set; } = 0;

        private const string CaliperPrefix   = "VisionPro.Caliper.";
        private const string DistancePrefix  = "VisionPro.CaliperDistance.";

        /// <summary>
        /// 두 Caliper 결과를 읽고 거리를 계산하여 context.Data에 저장합니다.
        /// </summary>
        public void Execute(VisionContext context)
        {
            string keyA       = CaliperPrefix + CaliperId_A;
            string keyB       = CaliperPrefix + CaliperId_B;
            string regionKeyA = keyA + ".Region";
            string regionKeyB = keyB + ".Region";

            // ── 결과 A 조회 ──
            object rawA;
            if (!context.Data.TryGetValue(keyA, out rawA))
            {
                context.SetError($"{Name}: Caliper[{CaliperId_A}] 결과가 없습니다. " +
                                 "이 스텝 이전에 Caliper 스텝이 실행되었는지 확인하세요.");
                return;
            }
            var resultsA = rawA as CogCaliperResults;
            if (resultsA == null || EdgeIndex_A >= resultsA.Count)
            {
                context.SetError($"{Name}: Caliper[{CaliperId_A}]에 에지[{EdgeIndex_A}]가 없습니다.");
                return;
            }

            // ── 결과 B 조회 ──
            object rawB;
            if (!context.Data.TryGetValue(keyB, out rawB))
            {
                context.SetError($"{Name}: Caliper[{CaliperId_B}] 결과가 없습니다. " +
                                 "이 스텝 이전에 Caliper 스텝이 실행되었는지 확인하세요.");
                return;
            }
            var resultsB = rawB as CogCaliperResults;
            if (resultsB == null || EdgeIndex_B >= resultsB.Count)
            {
                context.SetError($"{Name}: Caliper[{CaliperId_B}]에 에지[{EdgeIndex_B}]가 없습니다.");
                return;
            }

            // ── Region 조회 (없으면 null) ──
            object rawRegA, rawRegB;
            context.Data.TryGetValue(regionKeyA, out rawRegA);
            context.Data.TryGetValue(regionKeyB, out rawRegB);
            var regionA = rawRegA as CogRectangleAffine;
            var regionB = rawRegB as CogRectangleAffine;

            // ── 2D 좌표 변환 ──
            double x1, y1, x2, y2;
            ToImageXY(resultsA[EdgeIndex_A].Position, regionA, out x1, out y1);
            ToImageXY(resultsB[EdgeIndex_B].Position, regionB, out x2, out y2);

            // ── 결과 저장 (키 충돌 방지) ──
            int idx = 0;
            while (context.Data.ContainsKey(DistancePrefix + idx)) idx++;

            context.Data[DistancePrefix + idx] = new CaliperDistanceResult
            {
                X1          = x1,
                Y1          = y1,
                X2          = x2,
                Y2          = y2,
                CaliperId_A = CaliperId_A,
                EdgeIndex_A = EdgeIndex_A,
                CaliperId_B = CaliperId_B,
                EdgeIndex_B = EdgeIndex_B,
            };
        }

        /// <summary>
        /// Caliper 1D Position을 이미지 2D 좌표로 변환합니다.
        /// Region이 없으면 Position을 X, 0을 Y로 사용합니다.
        /// </summary>
        private static void ToImageXY(
            double position, CogRectangleAffine region,
            out double x, out double y)
        {
            if (region != null)
            {
                // Region 중심 + Rotation 방향으로 Position만큼 이동
                x = region.CenterX + Math.Cos(region.Rotation) * position;
                y = region.CenterY + Math.Sin(region.Rotation) * position;
            }
            else
            {
                x = position;
                y = 0;
            }
        }

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>CaliperId_A/B, EdgeIndex_A/B를 XML에 저장합니다.</summary>
        public void SaveParams(XElement el)
        {
            el.Add(
                new XElement("CaliperId_A",  CaliperId_A),
                new XElement("EdgeIndex_A",  EdgeIndex_A),
                new XElement("CaliperId_B",  CaliperId_B),
                new XElement("EdgeIndex_B",  EdgeIndex_B));
        }

        /// <summary>XML 요소에서 CaliperId_A/B, EdgeIndex_A/B를 복원합니다.</summary>
        public void LoadParams(XElement el)
        {
            CaliperId_A = Ri(el, "CaliperId_A", 0);
            EdgeIndex_A = Ri(el, "EdgeIndex_A", 0);
            CaliperId_B = Ri(el, "CaliperId_B", 1);
            EdgeIndex_B = Ri(el, "EdgeIndex_B", 0);
        }

        /// <summary>XML 요소에서 int 값을 읽습니다. 실패 시 def 반환.</summary>
        private static int Ri(XElement el, string n, int def)
        {
            var s = el.Element(n)?.Value;
            return s != null && int.TryParse(s, out var v) ? v : def;
        }
    }
}
