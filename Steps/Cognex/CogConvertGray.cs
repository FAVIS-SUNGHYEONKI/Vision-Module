using System.Xml.Linq;
using Cognex.VisionPro;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// CogImage24PlanarColor에서 R / G / B 단일 채널을 추출합니다.
    /// CogImage24PlanarColor.GetPlane(index)를 사용합니다.
    ///   Plane 0 = Red, Plane 1 = Green, Plane 2 = Blue
    ///
    /// 결과: context.CogImage = CogImage8Grey
    /// </summary>
    public class CogConvertGray : CogStepBase, IStepSerializable
    {
        public override string Name => "VisionPro.ConvertGray";

        public override ImageType RequiredInputType  => ImageType.Color;
        public override ImageType ProducedOutputType => ImageType.Grey;

        public enum ColorChannel { Red = 0, Green = 1, Blue = 2 }

        /// <summary>추출할 색상 채널 (기본값: Green)</summary>
        public ColorChannel Channel { get; set; } = ColorChannel.Green;

        // ── IStepSerializable ────────────────────────────────────────────

        public void SaveParams(XElement el)
        {
            el.Add(new XElement("Channel", (int)Channel));
        }

        public void LoadParams(XElement el)
        {
            var s = el.Element("Channel")?.Value;
            if (s != null && int.TryParse(s, out var v))
                Channel = (ColorChannel)v;
        }

        protected override void ExecuteCore(VisionContext context)
        {
            var colorImg = context.CogImage as CogImage24PlanarColor;
            if (colorImg == null)
            {
                context.SetError(
                    $"{Name}: CogImage24PlanarColor이 필요합니다. " +
                    $"현재 타입: {context.CogImage?.GetType().Name ?? "null"}");
                return;
            }

            var plane = colorImg.GetPlane((CogImagePlaneConstants)Channel);

            if (plane == null)
            {
                context.SetError($"{Name}: GetPlane({(int)Channel}) 실패.");
                return;
            }

            context.CogImage = plane;
            context.MatImage = null;
        }
    }
}
