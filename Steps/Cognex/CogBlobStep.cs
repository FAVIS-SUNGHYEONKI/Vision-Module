using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.Blob;

namespace Vision.Steps.VisionPro
{
    public class CogBlobStep : CogStepBase, IStepSerializable, IRegionStep
    {
        private readonly CogBlobTool _tool = new CogBlobTool();

        public override string Name => "VisionPro.Blob";

        public override ImageType RequiredInputType  => ImageType.Grey;
        public override ImageType ProducedOutputType => ImageType.Grey;

        public CogBlob RunParams => _tool.RunParams;

        public CogRectangleAffine Region
        {
            get => (CogRectangleAffine)_tool.Region;
            set => _tool.Region = value;
        }

        public bool RegionRequired => false;

        protected override void ExecuteCore(VisionContext context)
        {
            _tool.InputImage = context.CogImage;
            _tool.Run();

            var results = _tool.Results;
            if (results != null && results.GetBlobs().Count > 0)
                context.Data[Name] = results;
            else
                context.SetError($"{Name}: Blob 결과 없음.");
        }

        // ── IStepSerializable ────────────────────────────────────────────

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

        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        private static XElement Xi(string n, int v) => new XElement(n, v);

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
