using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageProcessing;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// CogImageConvertTool을 사용하여 컬러 이미지를 그레이스케일로 변환하는 스텝.
    ///
    /// RunMode = IntensityFromWeightedRGB로 설정하고
    /// R / G / B 채널 가중치(0.0~1.0)를 IntensityFromWeightedRGB* 프로퍼티에 반영한다.
    ///
    /// 입력: CogImage24PlanarColor (ImageType.Color)
    /// 출력: context.CogImage = CogImage8Grey (ImageType.Grey)
    ///
    /// 활용 예:
    ///   - 표준 휘도(BT.601): Red=0.299, Green=0.587, Blue=0.114
    ///   - 적색 채널 강조:    Red=1.0, Green=0.0, Blue=0.0
    /// </summary>
    public class CogWeightedRGBStep : CogStepBase, IStepSerializable
    {
        private readonly CogImageConvertTool _tool = new CogImageConvertTool();

        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "VisionPro.WeightedRGB";

        /// <summary>컬러 이미지(CogImage24PlanarColor)만 입력으로 받는다.</summary>
        public override ImageType RequiredInputType  => ImageType.Color;

        /// <summary>변환 후 그레이스케일 이미지를 출력한다.</summary>
        public override ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>적색 채널 가중치 (0.0~1.0). 기본값 1/3.</summary>
        public double RedWeight   { get; set; } = 1.0 / 3.0;

        /// <summary>녹색 채널 가중치 (0.0~1.0). 기본값 1/3.</summary>
        public double GreenWeight { get; set; } = 1.0 / 3.0;

        /// <summary>청색 채널 가중치 (0.0~1.0). 기본값 1/3.</summary>
        public double BlueWeight  { get; set; } = 1.0 / 3.0;

        public CogWeightedRGBStep()
        {
            _tool.RunParams.RunMode = CogImageConvertRunModeConstants.IntensityFromWeightedRGB;
        }

        // ── ExecuteCore ──────────────────────────────────────────────────

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

            _tool.RunParams.IntensityFromWeightedRGBRedWeight   = RedWeight;
            _tool.RunParams.IntensityFromWeightedRGBGreenWeight = GreenWeight;
            _tool.RunParams.IntensityFromWeightedRGBBlueWeight  = BlueWeight;

            _tool.InputImage = colorImg;
            _tool.Run();

            var result = _tool.OutputImage as CogImage8Grey;
            if (result == null)
            {
                context.SetError($"{Name}: 변환 결과 없음.");
                return;
            }

            context.CogImage = result;
            context.MatImage = null;

            // 출력 이미지 등록 — 다운스트림 스텝이 InputImageKey로 조회할 수 있도록
            if (context.CurrentStepIndex >= 0)
                context.RegisterImage("image:" + context.CurrentStepIndex, result);
        }

        // ── IStepSerializable ────────────────────────────────────────────

        public void SaveParams(XElement el)
        {
            el.Add(
                new XElement("RedWeight",   RedWeight.ToString("R",   CultureInfo.InvariantCulture)),
                new XElement("GreenWeight", GreenWeight.ToString("R", CultureInfo.InvariantCulture)),
                new XElement("BlueWeight",  BlueWeight.ToString("R",  CultureInfo.InvariantCulture)),
                new XElement("InputImageKey", InputImageKey ?? ""));
        }

        public void LoadParams(XElement el)
        {
            RedWeight   = Rd(el, "RedWeight",   1.0 / 3.0);
            GreenWeight = Rd(el, "GreenWeight", 1.0 / 3.0);
            BlueWeight  = Rd(el, "BlueWeight",  1.0 / 3.0);
            var keyEl = el.Element("InputImageKey");
            InputImageKey = keyEl != null && !string.IsNullOrEmpty(keyEl.Value) ? keyEl.Value : null;
        }

        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, System.Globalization.NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }
    }
}
