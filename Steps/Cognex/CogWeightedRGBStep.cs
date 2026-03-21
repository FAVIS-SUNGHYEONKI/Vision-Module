using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageProcessing;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// CogImage24PlanarColor의 R, G, B 채널에 가중치를 적용하여 그레이스케일로 합성한다.
    ///
    /// 처리 순서:
    ///   1. GetPlane(0/1/2)으로 각 채널을 CogImage8Grey로 추출
    ///   2. ICogImage8PixelMemory로 픽셀에 가중치 적용 → 새 CogImage8Grey 3장
    ///   3. CogImageArithmeticTool(Add)로 합산: (R*rW + G*gW) + B*bW
    ///
    /// 계산식: result = clip(R*RedWeight + G*GreenWeight + B*BlueWeight, 0, 255)
    ///
    /// 활용 예:
    ///   - 적색 강조:       Red=1.0, Green=0.0, Blue=0.0
    ///   - 표준 휘도(BT.601): Red=0.299, Green=0.587, Blue=0.114
    /// </summary>
    public class CogWeightedRGBStep : CogStepBase, IStepSerializable
    {
        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "VisionPro.WeightedRGB";

        /// <summary>컬러 이미지(CogImage24PlanarColor)만 입력으로 받는다.</summary>
        public override ImageType RequiredInputType  => ImageType.Color;

        /// <summary>가중 합산 후 그레이스케일 이미지를 출력한다.</summary>
        public override ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>적색 채널 가중치 (0.0~1.0). 기본값 1/3.</summary>
        public double RedWeight   { get; set; } = 1.0 / 3.0;

        /// <summary>녹색 채널 가중치 (0.0~1.0). 기본값 1/3.</summary>
        public double GreenWeight { get; set; } = 1.0 / 3.0;

        /// <summary>청색 채널 가중치 (0.0~1.0). 기본값 1/3.</summary>
        public double BlueWeight  { get; set; } = 1.0 / 3.0;

        // 채널별 가중 합산에 사용하는 CogImageArithmeticTool 인스턴스 (Add 연산)
        private readonly CogImageArithmeticTool _addRG    = new CogImageArithmeticTool();
        private readonly CogImageArithmeticTool _addFinal = new CogImageArithmeticTool();

        /// <summary>Add 연산자로 CogImageArithmeticTool 2개를 초기화한다.</summary>
        public CogWeightedRGBStep()
        {
            _addRG.RunParams.Operator    = CogImageArithmeticConstants.Add;
            _addFinal.RunParams.Operator = CogImageArithmeticConstants.Add;
        }

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>RedWeight, GreenWeight, BlueWeight를 XML에 저장한다.</summary>
        public void SaveParams(XElement el)
        {
            el.Add(
                new XElement("RedWeight",   RedWeight.ToString("R",   CultureInfo.InvariantCulture)),
                new XElement("GreenWeight", GreenWeight.ToString("R", CultureInfo.InvariantCulture)),
                new XElement("BlueWeight",  BlueWeight.ToString("R",  CultureInfo.InvariantCulture)));
        }

        /// <summary>XML 요소에서 RedWeight, GreenWeight, BlueWeight를 복원한다.</summary>
        public void LoadParams(XElement el)
        {
            RedWeight   = Rd(el, "RedWeight",   1.0 / 3.0);
            GreenWeight = Rd(el, "GreenWeight", 1.0 / 3.0);
            BlueWeight  = Rd(el, "BlueWeight",  1.0 / 3.0);
        }

        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        // ── ExecuteCore ──────────────────────────────────────────────────

        /// <summary>
        /// 컬러 이미지의 R/G/B 채널에 각 가중치를 적용하여 합산한 그레이스케일 이미지를 생성한다.
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

            var planeR = colorImg.GetPlane(0) as CogImage8Grey;
            var planeG = colorImg.GetPlane(1) as CogImage8Grey;
            var planeB = colorImg.GetPlane(2) as CogImage8Grey;

            if (planeR == null || planeG == null || planeB == null)
            {
                context.SetError($"{Name}: GetPlane() 실패.");
                return;
            }

            var weightedR = ApplyWeight(planeR, RedWeight);
            var weightedG = ApplyWeight(planeG, GreenWeight);
            var weightedB = ApplyWeight(planeB, BlueWeight);

            _addRG.InputImageA    = weightedR;
            _addRG.InputImageB    = weightedG;
            _addRG.Run();

            _addFinal.InputImageA = _addRG.OutputImage;
            _addFinal.InputImageB = weightedB;
            _addFinal.Run();

            context.CogImage = _addFinal.OutputImage as CogImage8Grey;
            context.MatImage = null;
        }

        /// <summary>
        /// CogImage8Grey 플레인의 각 픽셀에 weight를 곱한 새 CogImage8Grey를 반환한다.
        /// </summary>
        private static CogImage8Grey ApplyWeight(CogImage8Grey src, double weight)
        {
            int w = src.Width;
            int h = src.Height;

            var dst = new CogImage8Grey();
            dst.Allocate(w, h);

            var srcMem = src.Get8GreyPixelMemory(CogImageDataModeConstants.Read, 0, 0, w, h);
            var dstMem = dst.Get8GreyPixelMemory(CogImageDataModeConstants.ReadWrite, 0, 0, w, h);

            try
            {
                var srcRow = new byte[w];
                var dstRow = new byte[w];

                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(srcMem.Scan0 + y * srcMem.Stride, srcRow, 0, w);

                    for (int x = 0; x < w; x++)
                    {
                        double val = srcRow[x] * weight;
                        dstRow[x] = val >= 255.0 ? (byte)255 :
                                    val <= 0.0   ? (byte)0   : (byte)val;
                    }

                    Marshal.Copy(dstRow, 0, dstMem.Scan0 + y * dstMem.Stride, w);
                }
            }
            finally
            {
                (srcMem as IDisposable)?.Dispose();
                (dstMem as IDisposable)?.Dispose();
            }

            return dst;
        }
    }
}
