using System;
using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// 검출된 여러 에지 중 어느 것을 최종 결과로 선택할지 결정하는 모드.
    /// </summary>
    public enum CaliperSelectionMode
    {
        /// <summary>모든 에지를 반환한다.</summary>
        All,
        /// <summary>스캔 방향 기준 가장 앞쪽(Position 최솟값) 에지만 반환한다.</summary>
        FirstEdge,
        /// <summary>Score(신뢰도)가 가장 높은 에지만 반환한다.</summary>
        BestEdge,
    }

    /// <summary>
    /// VisionPro CogCaliperTool을 래핑하여 에지(Edge)를 검출하는 스텝.
    ///
    /// 동작:
    ///   - Region (CogRectangleAffine)을 스캔 영역으로 사용한다.
    ///   - Region이 없으면 전체 이미지를 스캔한다.
    ///   - 결과는 VisionContext.Data["VisionPro.Caliper.{N}"] 키에 저장된다.
    ///     같은 파이프라인에 Caliper 스텝이 여러 개 있으면 N이 자동으로 증가한다.
    ///
    /// 결과 타입: CogCaliperResultCollection
    ///   각 CogCaliperResult는 Position(1D 스캔축 위치), Score(0~1) 등을 포함한다.
    ///   2D 이미지 좌표 변환: imgX = Region.CenterX + cos(Rotation) * Position
    ///                        imgY = Region.CenterY + sin(Rotation) * Position
    ///
    /// 주요 설정:
    ///   - RunParams: ContrastThreshold, EdgeMode, Edge0Polarity, MaxResults 등
    ///   - Region: 스캔 영역 (중심 X/Y, 너비/높이, 회전각)
    ///   - SelectionMode: All / FirstEdge / BestEdge
    ///
    /// XML 직렬화 필드: Region(CenterX/Y, SideXLength/Y, Rotation, Skew),
    ///   RunParams(ContrastThreshold, EdgeMode, Edge0Polarity, FilterHalfSizeInPixels, MaxResults, SelectionMode)
    /// </summary>
    public class CogCaliperStep : CogStepBase, IStepSerializable, IRegionStep, IInspectionStep
    {
        private CogCaliperTool               _tool          = new CogCaliperTool();
        private CogCaliperScorerPositionNeg  _firstEdgeScorer = new CogCaliperScorerPositionNeg();
        private CogCaliperScorerContrast     _bestEdgeScorer  = new CogCaliperScorerContrast();

        /// <summary>스텝 고유 이름. VisionContext.Data 키 접두어로 사용된다.</summary>
        public override string Name => "VisionPro.Caliper";

        /// <summary>
        /// 내부 CogCaliperTool 인스턴스.
        /// 외부에서 미리 구성된 Tool을 주입할 때 사용한다.
        /// </summary>
        public CogCaliperTool Tool
        {
            get => _tool;
            set => _tool = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>에지 검출 파라미터 (ContrastThreshold, EdgeMode, Polarity 등).</summary>
        public CogCaliper RunParams => _tool.RunParams;

        /// <summary>스캔 영역. null이면 전체 이미지를 스캔한다.</summary>
        public CogRectangleAffine Region
        {
            get => _tool.Region;
            set => _tool.Region = value;
        }

        /// <summary>Caliper는 Region 없이도 실행 가능하다 (전체 이미지 스캔).</summary>
        public bool RegionRequired => false;

        /// <summary>
        /// 검출된 에지 중 최종 결과로 사용할 에지 선택 방식.
        /// All: 모든 에지, FirstEdge: 스캔 방향 첫 에지, BestEdge: 최고 점수 에지.
        /// </summary>
        public CaliperSelectionMode SelectionMode { get; set; } = CaliperSelectionMode.FirstEdge;

        /// <summary>
        /// Caliper 도구를 실행하고 결과를 컨텍스트에 기록한다.
        ///
        /// 결과 키: "VisionPro.Caliper.{N}" (같은 파이프라인에서 중복 방지를 위해 N 자동 증가)
        /// 결과 없음 시: context.SetError() 호출
        /// </summary>
        protected override void ExecuteCore(VisionContext context)
        {
            // SelectionMode에 맞는 scorer 구성 및 MaxResults 조정
            _tool.RunParams.SingleEdgeScorers.Clear();
            int savedMaxResults = _tool.RunParams.MaxResults;

            if (SelectionMode == CaliperSelectionMode.FirstEdge)
            {
                // 스캔 방향 기준 첫 번째 에지 — Position이 작을수록 높은 점수
                _tool.RunParams.SingleEdgeScorers.Add(_firstEdgeScorer);
                _tool.RunParams.MaxResults = 1;
            }
            else if (SelectionMode == CaliperSelectionMode.BestEdge)
            {
                // 대비(Contrast)가 가장 강한 에지 선택
                _tool.RunParams.SingleEdgeScorers.Add(_bestEdgeScorer);
                _tool.RunParams.MaxResults = 1;
            }

            _tool.InputImage = context.CogImage;
            _tool.Run();

            // MaxResults 원복 (SelectionMode 변경 후에도 원래 설정 유지)
            _tool.RunParams.MaxResults = savedMaxResults;

            if (_tool.Results != null && _tool.Results.Count > 0)
            {
                // 동일 파이프라인에 Caliper 스텝이 여러 개일 경우 키 충돌 방지
                int idx = 0;
                while (context.Data.ContainsKey(Name + "." + idx)) idx++;
                context.Data[Name + "." + idx] = _tool.Results;

                // CogCaliperDistanceStep이 2D 좌표 변환에 사용할 Region도 저장
                if (Region != null)
                    context.Data[Name + "." + idx + ".Region"] = Region;
            }
            else
                context.SetError($"{Name}: Caliper 결과 없음.");
        }

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>
        /// Region 및 RunParams를 XML 요소에 저장한다.
        /// InvariantCulture로 실수값을 직렬화하여 로케일 독립성을 보장한다.
        /// </summary>
        public void SaveParams(XElement el)
        {
            if (Region != null)
                el.Add(new XElement("Region",
                    Xd("CenterX",     Region.CenterX),
                    Xd("CenterY",     Region.CenterY),
                    Xd("SideXLength", Region.SideXLength),
                    Xd("SideYLength", Region.SideYLength),
                    Xd("Rotation",    Region.Rotation),
                    Xd("Skew",        Region.Skew)));

            el.Add(new XElement("RunParams",
                Xd("ContrastThreshold",      RunParams.ContrastThreshold),
                Xi("EdgeMode",               (int)RunParams.EdgeMode),
                Xi("Edge0Polarity",          (int)RunParams.Edge0Polarity),
                Xi("FilterHalfSizeInPixels", RunParams.FilterHalfSizeInPixels),
                Xi("MaxResults",             RunParams.MaxResults),
                Xi("SelectionMode",          (int)SelectionMode)));
        }

        /// <summary>
        /// XML 요소에서 Region 및 RunParams를 복원한다.
        /// 누락된 요소는 기본값으로 대체된다.
        /// </summary>
        public void LoadParams(XElement el)
        {
            var r = el.Element("Region");
            if (r != null && r.HasElements)
            {
                var region = new CogRectangleAffine();
                region.SetCenterLengthsRotationSkew(
                    Rd(r, "CenterX",     0),
                    Rd(r, "CenterY",     0),
                    Rd(r, "SideXLength", 100),
                    Rd(r, "SideYLength", 50),
                    Rd(r, "Rotation",    0),
                    Rd(r, "Skew",        0));
                Region = region;
            }

            var p = el.Element("RunParams");
            if (p == null) return;
            RunParams.ContrastThreshold      = Rd(p, "ContrastThreshold",      15.0);
            RunParams.EdgeMode               = (CogCaliperEdgeModeConstants)Ri(p, "EdgeMode",               0);
            RunParams.Edge0Polarity          = (CogCaliperPolarityConstants) Ri(p, "Edge0Polarity",          0);
            RunParams.FilterHalfSizeInPixels = Ri(p, "FilterHalfSizeInPixels", 2);
            RunParams.MaxResults             = Ri(p, "MaxResults",             1);
            SelectionMode                    = (CaliperSelectionMode)          Ri(p, "SelectionMode",         0);
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

        /// <summary>double 값을 InvariantCulture 문자열로 XML 요소 생성.</summary>
        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>int 값을 XML 요소로 생성.</summary>
        private static XElement Xi(string n, int v) =>
            new XElement(n, v);

        /// <summary>XML 요소에서 double 값을 읽는다. 실패 시 def 반환.</summary>
        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        /// <summary>XML 요소에서 int 값을 읽는다. 실패 시 def 반환.</summary>
        private static int Ri(XElement el, string n, int def)
        {
            var s = el.Element(n)?.Value;
            return s != null && int.TryParse(s, out var v) ? v : def;
        }
    }
}
