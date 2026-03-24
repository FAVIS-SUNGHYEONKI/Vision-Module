using System.Xml.Linq;
using Cognex.VisionPro;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// CogImage24PlanarColor에서 R / G / B 단일 채널을 추출하여 그레이스케일로 변환하는 스텝.
    ///
    /// CogImage24PlanarColor.GetPlane(index)를 사용한다:
    ///   Plane 0 = Red, Plane 1 = Green, Plane 2 = Blue
    ///
    /// 입력: 컬러 이미지 (ImageType.Color)
    /// 결과: context.CogImage = CogImage8Grey (선택한 채널의 강도 이미지)
    ///
    /// 활용 예:
    ///   - 조명 조건에 따라 특정 채널 선택 (적색 조명 → Red 채널)
    ///   - 표준 그레이 변환: WeightedRGB 스텝 권장 (BT.601 계수 사용)
    ///
    /// XML 직렬화 필드: Channel (0=Red, 1=Green, 2=Blue)
    /// </summary>
    public class CogConvertGrey : CogStepBase, IStepSerializable, IMultiChannelStep
    {
        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "VisionPro.ConvertGray";

        /// <summary>컬러 이미지(CogImage24PlanarColor)만 입력으로 받는다.</summary>
        public override ImageType RequiredInputType  => ImageType.Color;

        /// <summary>단일 채널 추출 후 그레이스케일 이미지를 출력한다.</summary>
        public override ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>추출할 색상 채널 (기본값: Green, Auto 불가)</summary>
        public ColorChannel Channel { get; set; } = ColorChannel.Green;

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>선택된 채널 인덱스와 InputImageKey를 XML에 저장한다.</summary>
        public void SaveParams(XElement el)
        {
            el.Add(new XElement("Channel", (int)Channel));
            el.Add(new XElement("InputImageKey", InputImageKey ?? ""));
        }

        /// <summary>XML 요소에서 채널 인덱스와 InputImageKey를 복원한다.</summary>
        public void LoadParams(XElement el)
        {
            var s = el.Element("Channel")?.Value;
            if (s != null && int.TryParse(s, out var v) && v >= 0)
                Channel = (ColorChannel)v;
            var keyEl = el.Element("InputImageKey");
            InputImageKey = keyEl != null && !string.IsNullOrEmpty(keyEl.Value) ? keyEl.Value : null;
        }

        /// <summary>
        /// 컬러 이미지에서 선택된 채널을 추출하여 context.CogImage를 업데이트한다.
        /// 입력이 CogImage24PlanarColor가 아니면 오류를 기록한다.
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

            // 선택된 채널을 파이프라인 기본 이미지로 등록
            string baseKey = "image:" + context.CurrentStepIndex;
            context.Images[baseKey] = plane;

            // R/G/B 3채널을 모두 등록 (다운스트림 스텝이 채널을 독립적으로 선택 가능)
            foreach (ColorChannel ch in new[] { ColorChannel.Red, ColorChannel.Green, ColorChannel.Blue })
            {
                var chPlane = colorImg.GetPlane((CogImagePlaneConstants)ch);
                context.Images[baseKey + "." + ch] = chPlane;
            }
        }
    }
}
