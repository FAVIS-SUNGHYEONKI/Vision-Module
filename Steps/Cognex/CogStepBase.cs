using Vision.Converters;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// VisionPro 기반 스텝의 추상 기반 클래스.
    ///
    /// CvImage만 있을 경우 자동으로 CogImage8Grey(VpImage)로 변환한 뒤 ExecuteCore를 호출합니다.
    /// 이를 통해 OpenCV 스텝 다음에 바로 VisionPro 스텝을 연결할 수 있습니다.
    /// </summary>
    public abstract class CogStepBase : IVisionStep, IImageTypedStep
    {
        public abstract string Name { get; }
        public virtual bool ContinueOnFailure => false;

        // Cognex 스텝 기본: 이미지를 그대로 통과 (분석만)
        // 하위 클래스에서 변환이 발생하면 override
        public virtual ImageType RequiredInputType  => ImageType.Any;
        public virtual ImageType ProducedOutputType => ImageType.Any;

        public void Execute(VisionContext context)
        {
            // CvImage → VpImage 자동 변환 (VpImage가 없을 때만)
            if (context.CogImage == null && context.MatImage != null)
            {
                context.CogImage = ImageConverter.ToCogImage8Grey(context.MatImage);
                // 변환 원본 MatImage는 더 이상 필요 없으므로 해제
                context.MatImage.Dispose();
                context.MatImage = null;
            }

            ExecuteCore(context);
        }

        protected abstract void ExecuteCore(VisionContext context);
    }
}
