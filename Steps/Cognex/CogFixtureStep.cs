using System;
using System.Globalization;
using System.Xml.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.CalibFix;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// VisionPro CogFixtureTool을 래핑하여 이미지를 픽스처(고정 좌표계)로 변환하는 스텝.
    ///
    /// 동작:
    ///   - 설정된 좌표 변환(ICogTransform2D)을 CogFixtureTool에 적용한다.
    ///   - 결과 CogFixturedImage를 context.CogImage로 교체하고 Images에도 등록한다.
    ///   - 이후 Caliper/Blob 스텝은 픽스처 좌표계에서 동작하게 된다.
    ///
    /// 변환 소스 우선순위:
    ///   1. TransformSourceKey가 설정된 경우: context.Data[key]에서 ICogTransform2D를 읽는다.
    ///      (예: PMAlign 결과 Pose — "VisionPro.PMAlign.0.Pose")
    ///   2. TransformSourceKey가 비어 있는 경우: TranslationX/Y, RotationDeg, Scaling 수동 값을 사용한다.
    ///
    /// XML 직렬화 필드: TranslationX, TranslationY, RotationDeg, Scaling,
    ///                  TransformSourceKey, InputImageKey
    /// </summary>
    public class CogFixtureStep : CogStepBase, IStepSerializable
    {
        private readonly CogFixtureTool _tool = new CogFixtureTool();

        /// <summary>스텝 고유 이름.</summary>
        public override string Name => "VisionPro.Fixture";

        /// <summary>어떤 이미지 타입도 입력으로 받는다.</summary>
        public override ImageType RequiredInputType  => ImageType.Any;

        /// <summary>Fixture 적용 후에도 동일 타입(CogFixturedImage)을 출력한다.</summary>
        public override ImageType ProducedOutputType => ImageType.Any;

        // ── 수동 변환 파라미터 ────────────────────────────────────────────

        /// <summary>X축 이동량 (pixels).</summary>
        public double TranslationX { get; set; } = 0.0;

        /// <summary>Y축 이동량 (pixels).</summary>
        public double TranslationY { get; set; } = 0.0;

        /// <summary>회전각 (degrees). 내부 실행 시 radians으로 변환된다.</summary>
        public double RotationDeg  { get; set; } = 0.0;

        /// <summary>균일 배율 (1.0 = 원본 크기).</summary>
        public double Scaling      { get; set; } = 1.0;

        // ── 런타임 변환 소스 ─────────────────────────────────────────────

        /// <summary>
        /// context.Data에서 ICogTransform2D를 읽어올 키.
        /// 설정하면 수동 TranslationX/Y/RotationDeg/Scaling 값을 무시하고 해당 변환을 사용한다.
        /// 예: "VisionPro.PMAlign.0.Pose" — PMAlign이 실행 시 저장하는 Pose 키
        /// </summary>
        public string TransformSourceKey { get; set; } = null;

        // ── ExecuteCore ──────────────────────────────────────────────────

        /// <summary>
        /// Fixture 변환을 실행한다.
        /// TransformSourceKey가 설정된 경우 context.Data에서 변환을 읽고,
        /// 없으면 수동 파라미터로 CogTransform2DLinear를 구성한다.
        /// </summary>
        protected override void ExecuteCore(VisionContext context)
        {
            if (context.CogImage == null)
            {
                context.SetError($"{Name}: 입력 이미지가 없습니다.");
                return;
            }

            ICogTransform2D transform = null;

            // ① 런타임 소스에서 변환 취득
            if (!string.IsNullOrEmpty(TransformSourceKey))
            {
                object raw;
                if (context.Data.TryGetValue(TransformSourceKey, out raw))
                    transform = raw as ICogTransform2D;

                if (transform == null)
                {
                    context.SetError(
                        $"{Name}: TransformSourceKey '{TransformSourceKey}'에서 " +
                        "ICogTransform2D를 찾을 수 없습니다.");
                    return;
                }
            }

            // ② 수동 파라미터로 변환 구성
            if (transform == null)
            {
                var manual = new CogTransform2DLinear();
                manual.TranslationX = TranslationX;
                manual.TranslationY = TranslationY;
                manual.Rotation     = RotationDeg * Math.PI / 180.0;
                manual.Scaling      = Scaling;
                transform = manual;
            }

            _tool.InputImage                = context.CogImage;
            _tool.RunParams.FixtureTransform = transform;
            _tool.Run();

            if (_tool.OutputImage == null)
            {
                context.SetError($"{Name}: Fixture 출력 이미지가 없습니다.");
                return;
            }

            context.CogImage = _tool.OutputImage;
            context.RegisterImage("image:" + context.CurrentStepIndex, context.CogImage);
        }

        // ── IStepSerializable ────────────────────────────────────────────

        /// <summary>수동 변환 파라미터 및 키 값을 XML에 저장한다.</summary>
        public void SaveParams(XElement el)
        {
            el.Add(
                Xd("TranslationX",        TranslationX),
                Xd("TranslationY",        TranslationY),
                Xd("RotationDeg",         RotationDeg),
                Xd("Scaling",             Scaling),
                new XElement("TransformSourceKey", TransformSourceKey ?? ""),
                new XElement("InputImageKey",       InputImageKey      ?? ""));
        }

        /// <summary>XML 요소에서 파라미터를 복원한다.</summary>
        public void LoadParams(XElement el)
        {
            TranslationX = Rd(el, "TranslationX", 0.0);
            TranslationY = Rd(el, "TranslationY", 0.0);
            RotationDeg  = Rd(el, "RotationDeg",  0.0);
            Scaling      = Rd(el, "Scaling",       1.0);

            var tkEl = el.Element("TransformSourceKey");
            TransformSourceKey = tkEl != null && !string.IsNullOrEmpty(tkEl.Value)
                ? tkEl.Value : null;

            var keyEl = el.Element("InputImageKey");
            InputImageKey = keyEl != null && !string.IsNullOrEmpty(keyEl.Value)
                ? keyEl.Value : null;
        }

        // ── XML 헬퍼 ─────────────────────────────────────────────────────

        private static XElement Xd(string n, double v) =>
            new XElement(n, v.ToString("R", CultureInfo.InvariantCulture));

        private static double Rd(XElement el, string n, double def)
        {
            var s = el.Element(n)?.Value;
            return s != null && double.TryParse(s, NumberStyles.Any,
                CultureInfo.InvariantCulture, out var v) ? v : def;
        }
    }
}
