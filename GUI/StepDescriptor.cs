using System;

namespace Vision.UI
{
    /// <summary>
    /// PipelineController와 PipelineEditorForm에서 사용하는 스텝 유형 메타데이터.
    /// 팩토리 패턴으로 스텝 인스턴스를 생성합니다.
    ///
    /// TypeName은 step.Name과 일치하며, 파이프라인 XML 저장/복원에 사용됩니다.
    /// </summary>
    public class StepDescriptor
    {
        /// <summary>UI에 표시할 이름 (예: "Caliper").</summary>
        public string    DisplayName        { get; }

        /// <summary>스텝 분류 ("Cognex" 또는 "OpenCV").</summary>
        public string    Category           { get; }

        /// <summary>스텝 고유 이름 — step.Name과 동일 (예: "VisionPro.Caliper").</summary>
        public string    TypeName           { get; }

        /// <summary>스텝이 요구하는 입력 이미지 타입.</summary>
        public ImageType RequiredInputType  { get; }

        /// <summary>스텝이 생성하는 출력 이미지 타입.</summary>
        public ImageType ProducedOutputType { get; }

        private readonly Func<IVisionStep> _factory;

        /// <param name="displayName">UI 목록에 표시할 이름.</param>
        /// <param name="category">"Cognex" 또는 "OpenCV".</param>
        /// <param name="factory">스텝 인스턴스를 생성하는 팩토리 함수.</param>
        public StepDescriptor(string displayName, string category, Func<IVisionStep> factory)
        {
            DisplayName = displayName;
            Category    = category;
            _factory    = factory;

            var temp = factory();
            TypeName           = temp.Name;
            var typed          = temp as IImageTypedStep;
            RequiredInputType  = typed?.RequiredInputType  ?? ImageType.Any;
            ProducedOutputType = typed?.ProducedOutputType ?? ImageType.Any;
            (temp as IDisposable)?.Dispose();
        }

        /// <summary>새 스텝 인스턴스를 생성합니다.</summary>
        public IVisionStep CreateStep() => _factory();

        public override string ToString()
            => $"[{Category}] {DisplayName}  ({TypeLabel(RequiredInputType)}->{TypeLabel(ProducedOutputType)})";

        internal static string TypeLabel(ImageType t)
        {
            switch (t)
            {
                case ImageType.Grey:  return "Grey";
                case ImageType.Color: return "Color";
                default:              return "Any";
            }
        }
    }
}
