using System.Xml.Linq;
using Cognex.VisionPro;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// CogImage24PlanarColor에서 R / G / B 단일 채널을 추출하여 그레이스케일로 변환하는 스텝.
    ///
    /// CogImage24PlanarColor.GetPlane(index)를 사용합니다:
    ///   Plane 0 = Red, Plane 1 = Green, Plane 2 = Blue
    ///
    /// 입력: 컬러 이미지 (ImageType.Color)
    /// 결과: context.CogImage = CogImage8Grey (선택한 채널의 강도 이미지)
    ///
    /// 활용 예:
    ///   - 조명 조건에 따라 특정 채널 선택 (적색 조명 → Red 채널)
    ///   - 표준 그레이 변환: WeightedRGB 스텝 권장 (BT.601 계수 사용)
    ///
    /// XML 직렬화:
    ///   &lt;Step type="VisionPro.ConvertGray"&gt;
    ///     &lt;Channel&gt;1&lt;/Channel&gt;  &lt;!-- 0=Red, 1=Green, 2=Blue --&gt;
    ///   &lt;/Step&gt;
    /// </summary>
    public class CogConvertGray : CogStepBase, IStepSerializable
    {
        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "VisionPro.ConvertGray";

        /// <summary>컬러 이미지(CogImage24PlanarColor)만 입력으로 받습니다.</summary>
        public override ImageType RequiredInputType  => ImageType.Color;

        /// <summary>단일 채널 추출 후 그레이스케일 이미지를 출력합니다.</summary>
        public override ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>추출 가능한 색상 채널 열거형.</summary>
        public enum ColorChannel { Red = 0, Green = 1, Blue = 2 }

        /// <summary>추출할 색상 채널 (기본값: Green)</summary>
        public ColorChannel Channel { get; set; } = ColorChannel.Green;

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>선택된 채널 인덱스를 XML에 저장합니다.</summary>
        public void SaveParams(XElement el)
        {
            el.Add(new XElement("Channel", (int)Channel));
        }

        /// <summary>XML 요소에서 채널 인덱스를 복원합니다.</summary>
        public void LoadParams(XElement el)
        {
            var s = el.Element("Channel")?.Value;
            if (s != null && int.TryParse(s, out var v))
                Channel = (ColorChannel)v;
        }

        /// <summary>
        /// 컬러 이미지에서 선택된 채널을 추출하여 context.CogImage를 업데이트합니다.
        /// 입력이 CogImage24PlanarColor가 아니면 오류를 기록합니다.
        /// </summary>
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
