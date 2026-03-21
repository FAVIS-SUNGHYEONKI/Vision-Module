using System.Globalization;
using System.Xml.Linq;
using OpenCvSharp;

namespace Vision.Steps.OpenCV
{
    /// <summary>
    /// OpenCV Cv2.Threshold를 사용하여 그레이스케일 이미지를 이진화하는 스텝.
    ///
    /// 동작:
    ///   - context.MatImage에 대해 Cv2.Threshold()를 적용한다.
    ///   - 결과 Mat으로 context.MatImage를 교체한다 (원본 Mat은 즉시 해제).
    ///   - 실행 후 context.CogImage는 null이 된다.
    ///
    /// 주요 ThresholdTypes:
    ///   - Binary     : pixel > thresh → MaxValue, 그 외 → 0
    ///   - BinaryInv  : pixel > thresh → 0, 그 외 → MaxValue
    ///   - Otsu       : 자동 최적 임계값 (ThresholdValue 무시)
    ///   - Triangle   : 삼각형 알고리즘 자동 임계값
    ///
    /// XML 직렬화 필드: ThresholdValue, MaxValue, Type
    /// </summary>
    public class CvThresholdStep : CvStepBase, IStepSerializable
    {
        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "OpenCV.Threshold";

        /// <summary>이진화 임계값 (0~255). Otsu/Triangle 타입에서는 무시된다.</summary>
        public double         ThresholdValue { get; set; } = 128.0;

        /// <summary>임계값 초과 픽셀에 설정할 최대값 (보통 255).</summary>
        public double         MaxValue       { get; set; } = 255.0;

        /// <summary>이진화 방식 (Binary, BinaryInv, Otsu 등).</summary>
        public ThresholdTypes Type           { get; set; } = ThresholdTypes.Binary;

        /// <summary>
        /// 이진화를 실행한다.
        /// 결과 Mat으로 context.MatImage를 교체하고 context.CogImage를 null로 설정한다.
        /// </summary>
        protected override void ExecuteCore(VisionContext context)
        {
            if (context.MatImage == null || context.MatImage.Empty())
            {
                context.SetError($"{Name}: CvImage가 비어 있습니다.");
                return;
            }

            var result = new Mat();
            Cv2.Threshold(context.MatImage, result, ThresholdValue, MaxValue, Type);

            // 원본 Mat 해제 후 결과로 교체
            context.MatImage.Dispose();
            context.MatImage = result;
            (context.CogImage as System.IDisposable)?.Dispose();
            context.CogImage = null;
        }

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>ThresholdValue, MaxValue, Type을 XML에 저장한다.</summary>
        public void SaveParams(XElement el)
        {
            el.Add(
                Xd("ThresholdValue", ThresholdValue),
                Xd("MaxValue",       MaxValue),
                Xi("Type",           (int)Type));
        }

        /// <summary>XML 요소에서 ThresholdValue, MaxValue, Type을 복원한다.</summary>
        public void LoadParams(XElement el)
        {
            ThresholdValue = Rd(el, "ThresholdValue", 128.0);
            MaxValue       = Rd(el, "MaxValue",       255.0);
            Type           = (ThresholdTypes)Ri(el, "Type", (int)ThresholdTypes.Binary);
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

        /// <summary>double 값을 InvariantCulture 문자열로 XML 요소 생성.</summary>
        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        /// <summary>int 값을 XML 요소로 생성.</summary>
        private static XElement Xi(string n, int v) => new XElement(n, v);

        /// <summary>XML 요소에서 double 값을 읽는다. 실패 시 def 반환.</summary>
        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, System.Globalization.NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }

        /// <summary>XML 요소에서 int 값을 읽는다. 실패 시 def 반환.</summary>
        private static int Ri(XElement el, string n, int def)
        {
            var s = el.Element(n)?.Value;
            return s != null && int.TryParse(s, out var v) ? v : def;
        }
    }
}
