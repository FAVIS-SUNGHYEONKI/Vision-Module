using System.Globalization;
using System.Xml.Linq;
using OpenCvSharp;

namespace Vision.Steps.OpenCV
{
    public class CvThresholdStep : CvStepBase, IStepSerializable
    {
        public override string Name => "OpenCV.Threshold";

        public double         ThresholdValue { get; set; } = 128.0;
        public double         MaxValue       { get; set; } = 255.0;
        public ThresholdTypes Type           { get; set; } = ThresholdTypes.Binary;

        protected override void ExecuteCore(VisionContext context)
        {
            if (context.MatImage == null || context.MatImage.Empty())
            {
                context.SetError($"{Name}: CvImage가 비어 있습니다.");
                return;
            }

            var result = new Mat();
            Cv2.Threshold(context.MatImage, result, ThresholdValue, MaxValue, Type);

            context.MatImage.Dispose();
            context.MatImage = result;
            (context.CogImage as System.IDisposable)?.Dispose();
            context.CogImage = null;
        }

        // ── IStepSerializable ────────────────────────────────────────────

        public void SaveParams(XElement el)
        {
            el.Add(
                Xd("ThresholdValue", ThresholdValue),
                Xd("MaxValue",       MaxValue),
                Xi("Type",           (int)Type));
        }

        public void LoadParams(XElement el)
        {
            ThresholdValue = Rd(el, "ThresholdValue", 128.0);
            MaxValue       = Rd(el, "MaxValue",       255.0);
            Type           = (ThresholdTypes)Ri(el, "Type", (int)ThresholdTypes.Binary);
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        private static XElement Xi(string n, int v) => new XElement(n, v);

        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, System.Globalization.NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        private static int Ri(XElement el, string n, int def)
        {
            var s = el.Element(n)?.Value;
            return s != null && int.TryParse(s, out var v) ? v : def;
        }
    }
}
