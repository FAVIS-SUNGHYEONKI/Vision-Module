#if PMALIGN_ENABLED
using System;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.PMAlign;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// VisionPro CogPMAlignTool을 래핑하여 패턴 매칭(위치/각도/스케일)을 수행하는 스텝.
    ///
    /// 동작:
    ///   1. TrainPattern() 호출로 기준 패턴을 학습한다.
    ///   2. Execute 시 입력 이미지에서 학습된 패턴을 검색한다.
    ///   3. Region이 설정되어 있으면 해당 영역만 검색한다.
    ///   4. 결과는 VisionContext.Data["VisionPro.PMAlign.{N}"] 키에 저장된다.
    ///
    /// 결과 타입: CogPMAlignResults
    ///   각 결과는 X/Y 위치, Angle(rad), Scale, Score(0~1) 를 포함한다.
    ///
    /// 주요 설정:
    ///   - RunParams.AcceptThreshold : 최소 허용 점수 (0~1, 기본 0.5)
    ///   - RunParams.MaxResults      : 최대 검출 수 (기본 1)
    ///   - RunParams.ZoneAngle       : 검색 각도 범위 (Low/High, radians)
    ///   - RunParams.ZoneScale       : 검색 스케일 범위 (Low/High)
    ///
    /// XML 직렬화: AcceptThreshold, MaxResults, ZoneAngle, ZoneScale, TrainRegion,
    ///            학습된 패턴(Base64 인코딩된 CogSerializer 직렬화 데이터)
    /// </summary>
    public class CogPMAlignStep : CogStepBase, IStepSerializable, IRegionStep, IInspectionStep
    {
        private readonly CogPMAlignTool _tool = new CogPMAlignTool();

        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "VisionPro.PMAlign";

        /// <summary>PMAlign은 어떤 이미지 타입도 입력으로 받는다.</summary>
        public override ImageType RequiredInputType  => ImageType.Any;

        /// <summary>이미지를 변환하지 않는다.</summary>
        public override ImageType ProducedOutputType => ImageType.Any;

        // ── 속성 ────────────────────────────────────────────────────────

        /// <summary>
        /// 실행 시 검색할 영역(ROI). null이면 전체 이미지를 검색한다.
        /// IRegionStep 구현 — PipelineEditorForm의 영역 설정 기능과 연동된다.
        /// </summary>
        public CogRectangleAffine Region
        {
            get => _tool.SearchRegion as CogRectangleAffine;
            set
            {
                _tool.SearchRegion = value;
            }
        }

        /// <summary>PMAlign은 Region 없이도 실행 가능하다 (전체 이미지 검색).</summary>
        public bool RegionRequired => false;

        /// <summary>패턴 학습에 사용할 학습 영역. TrainPattern() 호출 전에 설정한다.</summary>
        public CogRectangleAffine TrainRegion { get; set; }

        /// <summary>패턴 매칭 파라미터 (AcceptThreshold, ZoneAngle, ZoneScale 등).</summary>
        public CogPMAlignRunParams RunParams => _tool.RunParams;

        /// <summary>최대 검출 수. RunParams가 아닌 Tool 직접 프로퍼티.</summary>
        public int MaxResults
        {
            get => _tool.MaxResults;
            set => _tool.MaxResults = value;

        }

        /// <summary>패턴이 학습되어 있으면 true.</summary>
        public bool IsPatternTrained => _tool.Pattern != null && _tool.Pattern.Trained;

        // ── 학습 ────────────────────────────────────────────────────────

        /// <summary>
        /// 입력 이미지와 학습 영역으로 패턴을 학습한다.
        ///
        /// trainRegion이 null이면 this.TrainRegion을 사용한다.
        /// 학습이 완료되면 this.TrainRegion이 사용된 영역으로 업데이트된다.
        /// </summary>
        /// <param name="image">학습에 사용할 이미지</param>
        /// <param name="trainRegion">학습 영역. null이면 this.TrainRegion 사용</param>
        public void TrainPattern(ICogImage image, CogRectangleAffine trainRegion = null)
        {
            if (image == null) throw new ArgumentNullException("image");

            var region = trainRegion ?? TrainRegion;
            _tool.Pattern.Train(image, region,
                CogPMAlignPatternTrainModeConstants.PatternAndInspection);

            if (region != null)
                TrainRegion = region;

        }

        // ── ExecuteCore ──────────────────────────────────────────────────

        protected override void ExecuteCore(VisionContext context)
        {
            if (!IsPatternTrained)
            {
                context.SetError($"{Name}: 패턴이 학습되지 않았습니다. 편집기에서 '패턴 학습'을 먼저 실행하세요.");
                return;
            }

            _tool.InputImage = context.CogImage;
            _tool.Run();

            var results = _tool.Results;
            if (results != null && results.Count > 0)
            {
                int idx = 0;
                while (context.Data.ContainsKey(Name + "." + idx)) idx++;
                context.Data[Name + "." + idx] = results;
            }
            else
            {
                context.SetError($"{Name}: PMAlign 결과 없음 (AcceptThreshold={RunParams.AcceptThreshold:F2}).");
            }
        }

        // ── IStepSerializable ────────────────────────────────────────────

        public void SaveParams(XElement el)
        {
            // RunParams
            el.Add(new XElement("RunParams",
                Xd("AcceptThreshold", RunParams.AcceptThreshold),
                Xi("MaxResults",      _tool.MaxResults),
                Xd("ZoneAngleLow",   RunParams.ZoneAngle.Low),
                Xd("ZoneAngleHigh",  RunParams.ZoneAngle.High),
                Xd("ZoneScaleLow",   RunParams.ZoneScale.Low),
                Xd("ZoneScaleHigh",  RunParams.ZoneScale.High)));

            // 학습 영역
            if (TrainRegion != null)
                el.Add(new XElement("TrainRegion",
                    Xd("CenterX",     TrainRegion.CenterX),
                    Xd("CenterY",     TrainRegion.CenterY),
                    Xd("SideXLength", TrainRegion.SideXLength),
                    Xd("SideYLength", TrainRegion.SideYLength),
                    Xd("Rotation",    TrainRegion.Rotation),
                    Xd("Skew",        TrainRegion.Skew)));

            // 검색 영역
            var searchRegion = Region;
            if (searchRegion != null)
                el.Add(new XElement("SearchRegion",
                    Xd("CenterX",     searchRegion.CenterX),
                    Xd("CenterY",     searchRegion.CenterY),
                    Xd("SideXLength", searchRegion.SideXLength),
                    Xd("SideYLength", searchRegion.SideYLength),
                    Xd("Rotation",    searchRegion.Rotation),
                    Xd("Skew",        searchRegion.Skew)));

            // 학습된 패턴 직렬화 (Base64)
            if (IsPatternTrained)
            {
                try
                {
                    using (var ms = new MemoryStream())
                    {
                        CogSerializer.SaveObjectToStream(
                            _tool.Pattern, ms,
                            CogSerializationOptionsConstants.None);
                        el.Add(new XElement("PatternData",
                            Convert.ToBase64String(ms.ToArray())));
                    }
                }
                catch { /* 패턴 직렬화 실패 시 무시 — 재학습 필요 */ }
            }
        }

        public void LoadParams(XElement el)
        {
            // RunParams
            var p = el.Element("RunParams");
            if (p != null)
            {
                RunParams.AcceptThreshold = Rd(p, "AcceptThreshold", 0.5);
                _tool.MaxResults          = Ri(p, "MaxResults", 1);
                RunParams.ZoneAngle.Low       = Rd(p, "ZoneAngleLow",  -Math.PI);
                RunParams.ZoneAngle.High      = Rd(p, "ZoneAngleHigh",  Math.PI);
                RunParams.ZoneScale.Low       = Rd(p, "ZoneScaleLow",  1.0);
                RunParams.ZoneScale.High      = Rd(p, "ZoneScaleHigh", 1.0);
            }

            // 학습 영역
            TrainRegion = LoadRectangle(el.Element("TrainRegion"));

            // 검색 영역
            Region = LoadRectangle(el.Element("SearchRegion"));

            // 학습된 패턴 복원
            var patternEl = el.Element("PatternData");
            if (patternEl != null && !string.IsNullOrEmpty(patternEl.Value))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(patternEl.Value);
                    using (var ms = new MemoryStream(bytes))
                    {
                        var pattern = CogSerializer.LoadObjectFromStream(ms) as CogPMAlignPattern;
                        if (pattern != null)
                            _tool.Pattern = pattern;
                    }
                }
                catch { /* 패턴 복원 실패 시 무시 — 재학습 필요 */ }
            }
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

        private static CogRectangleAffine LoadRectangle(XElement r)
        {
            if (r == null || !r.HasElements) return null;
            var region = new CogRectangleAffine();
            region.SetCenterLengthsRotationSkew(
                Rd(r, "CenterX",     0),
                Rd(r, "CenterY",     0),
                Rd(r, "SideXLength", 100),
                Rd(r, "SideYLength", 100),
                Rd(r, "Rotation",    0),
                Rd(r, "Skew",        0));
            return region;
        }

        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        private static XElement Xi(string n, int v) =>
            new XElement(n, v);

        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private static int Ri(XElement el, string n, int def)
        {
            var s = el.Element(n)?.Value;
            return s != null && int.TryParse(s, out var v) ? v : def;
        }
    }
}
#endif // PMALIGN_ENABLED
