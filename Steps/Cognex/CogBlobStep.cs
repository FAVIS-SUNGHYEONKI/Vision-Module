using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.Blob;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// VisionPro CogBlobTool을 래핑하여 연결된 픽셀 영역(Blob)을 검출하는 스텝.
    ///
    /// 동작:
    ///   - 그레이스케일 이미지를 임계값으로 이진화하여 Blob을 검출합니다.
    ///   - <see cref="Region"/> (CogRectangleAffine)을 검색 영역으로 사용합니다.
    ///   - 결과는 VisionContext.Data["VisionPro.Blob.{N}"] 키에 저장됩니다.
    ///     같은 파이프라인에 Blob 스텝이 여러 개 있으면 N이 자동으로 증가합니다.
    ///
    /// 결과 타입: CogBlobResultCollection
    ///   각 CogBlobResult는 Area, Centroid, GetBoundary() 등을 제공합니다.
    ///   GetBoundary()로 CogPolygon(외곽선)을 얻어 Display에 직접 추가할 수 있습니다.
    ///
    /// 세그멘테이션 모드 (RunParams.SegmentationParams.Mode):
    ///   - HardFixedThreshold  : 단일 고정 임계값
    ///   - SoftFixedThreshold  : 상/하한 임계값 범위 (Low ~ High)
    ///   - HardRelativeThreshold: 이미지 평균 기준 상대 임계값
    ///   - SoftRelativeThreshold: 이미지 평균 기준 상대 상/하한
    ///
    /// 입력: 그레이스케일 이미지 (ImageType.Grey)
    ///
    /// XML 직렬화:
    ///   &lt;Step type="VisionPro.Blob"&gt;
    ///     &lt;Region&gt;CenterX, CenterY, SideXLength, SideYLength, Rotation, Skew&lt;/Region&gt;
    ///     &lt;SegmentationParams&gt;Mode, Polarity, HardFixedThreshold, ...&lt;/SegmentationParams&gt;
    ///     &lt;ConnectivityMinPixels&gt;100&lt;/ConnectivityMinPixels&gt;
    ///   &lt;/Step&gt;
    /// </summary>
    public class CogBlobStep : CogStepBase, IStepSerializable, IRegionStep, IInspectionStep
    {
        private readonly CogBlobTool _tool = new CogBlobTool();

        /// <summary>스텝 고유 이름. VisionContext.Data 키로 사용됩니다.</summary>
        public override string Name => "VisionPro.Blob";

        /// <summary>Blob 검출은 그레이스케일 이미지를 입력으로 요구합니다.</summary>
        public override ImageType RequiredInputType  => ImageType.Grey;

        /// <summary>Blob 검출 후에도 이미지는 그레이스케일로 유지됩니다.</summary>
        public override ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>
        /// Blob 검출 파라미터.
        /// SegmentationParams (Mode, Polarity, Threshold 등) 및
        /// ConnectivityMinPixels (최소 픽셀 수 필터) 포함.
        /// </summary>
        public CogBlob RunParams => _tool.RunParams;

        /// <summary>검색 영역. null이면 전체 이미지를 검색합니다.</summary>
        public CogRectangleAffine Region
        {
            get => (CogRectangleAffine)_tool.Region;
            set => _tool.Region = value;
        }

        /// <summary>Blob은 Region 없이도 실행 가능합니다 (전체 이미지 검색).</summary>
        public bool RegionRequired => false;

        /// <summary>
        /// Blob 도구를 실행하고 결과를 컨텍스트에 기록합니다.
        ///
        /// 결과 키: "VisionPro.Blob.{N}" (같은 파이프라인에서 중복 방지를 위해 N 자동 증가)
        /// Blob이 하나도 검출되지 않으면 context.SetError() 호출.
        /// </summary>
        protected override void ExecuteCore(VisionContext context)
        {
            _tool.InputImage = context.CogImage;
            _tool.Run();

            var results = _tool.Results;
            if (results != null && results.GetBlobs().Count > 0)
            {
                // 동일 파이프라인에 Blob 스텝이 여러 개일 경우 키 충돌 방지
                int idx = 0;
                while (context.Data.ContainsKey(Name + "." + idx)) idx++;
                context.Data[Name + "." + idx] = results;
            }
            else
                context.SetError($"{Name}: Blob 결과 없음.");
        }

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>
        /// Region, SegmentationParams, ConnectivityMinPixels를 XML에 저장합니다.
        /// InvariantCulture로 실수값을 직렬화하여 로케일 독립성을 보장합니다.
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

            var seg = RunParams.SegmentationParams;
            el.Add(new XElement("SegmentationParams",
                Xi("Mode",                   (int)seg.Mode),
                Xi("Polarity",               (int)seg.Polarity),
                Xd("HardFixedThreshold",     seg.HardFixedThreshold),
                Xd("SoftFixedThresholdLow",  seg.SoftFixedThresholdLow),
                Xd("SoftFixedThresholdHigh", seg.SoftFixedThresholdHigh)));

            el.Add(new XElement("ConnectivityMinPixels", RunParams.ConnectivityMinPixels));
        }

        /// <summary>
        /// XML 요소에서 Region, SegmentationParams, ConnectivityMinPixels를 복원합니다.
        /// 누락된 요소는 기본값으로 대체됩니다.
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
                    Rd(r, "SideYLength", 100),
                    Rd(r, "Rotation",    0),
                    Rd(r, "Skew",        0));
                Region = region;
            }

            var segEl = el.Element("SegmentationParams");
            if (segEl != null)
            {
                var seg = RunParams.SegmentationParams;
                seg.Mode                   = (CogBlobSegmentationModeConstants)  Ri(segEl, "Mode",                   (int)CogBlobSegmentationModeConstants.HardFixedThreshold);
                seg.Polarity               = (CogBlobSegmentationPolarityConstants)Ri(segEl, "Polarity",             (int)CogBlobSegmentationPolarityConstants.LightBlobs);
                seg.HardFixedThreshold     = Ri(segEl, "HardFixedThreshold",     128);
                seg.SoftFixedThresholdLow  = Ri(segEl, "SoftFixedThresholdLow",  100);
                seg.SoftFixedThresholdHigh = Ri(segEl, "SoftFixedThresholdHigh", 200);
            }

            var minPxEl = el.Element("ConnectivityMinPixels");
            if (minPxEl != null && int.TryParse(minPxEl.Value, out var minPx))
                RunParams.ConnectivityMinPixels = minPx;
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

        /// <summary>double 값을 InvariantCulture 문자열로 XML 요소 생성.</summary>
        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>int 값을 XML 요소로 생성.</summary>
        private static XElement Xi(string n, int v) => new XElement(n, v);

        /// <summary>XML 요소에서 double 값을 읽습니다. 실패 시 def 반환.</summary>
        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        /// <summary>XML 요소에서 int 값을 읽습니다. 실패 시 def 반환.</summary>
        private static int Ri(XElement el, string n, int def)
        {
            var s = el.Element(n)?.Value;
            return s != null && int.TryParse(s, out var v) ? v : def;
        }
    }
}
