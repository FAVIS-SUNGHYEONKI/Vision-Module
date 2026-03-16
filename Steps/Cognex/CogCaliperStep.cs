using System;
using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;

namespace Vision.Steps.VisionPro
{
    public class CogCaliperStep : CogStepBase, IStepSerializable, IRegionStep
    {
        private CogCaliperTool _tool = new CogCaliperTool();

        public override string Name => "VisionPro.Caliper";

        public CogCaliperTool Tool
        {
            get => _tool;
            set => _tool = value ?? throw new ArgumentNullException(nameof(value));
        }

        public CogCaliper RunParams => _tool.RunParams;

        public CogRectangleAffine Region
        {
            get => _tool.Region;
            set => _tool.Region = value;
        }

        /// <summary>Caliper는 Region 없이도 실행 가능합니다 (전체 이미지 스캔).</summary>
        public bool RegionRequired => false;

        protected override void ExecuteCore(VisionContext context)
        {
            _tool.InputImage = context.CogImage;
            _tool.Run();

            if (_tool.Results != null && _tool.Results.Count > 0)
                context.Data[Name] = _tool.Results;
            else
                context.SetError($"{Name}: Caliper 결과 없음.");
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

            el.Add(new XElement("RunParams",
                Xd("ContrastThreshold",      RunParams.ContrastThreshold),
                Xi("EdgeMode",               (int)RunParams.EdgeMode),
                Xi("Edge0Polarity",          (int)RunParams.Edge0Polarity),
                Xi("FilterHalfSizeInPixels", RunParams.FilterHalfSizeInPixels),
                Xi("MaxResults",             RunParams.MaxResults)));
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
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

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
