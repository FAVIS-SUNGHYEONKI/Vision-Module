using System;
using Vision.Converters;

namespace Vision.Steps.OpenCV
{
    /// <summary>
    /// OpenCV 기반 스텝의 추상 기반 클래스.
    ///
    /// VpImage만 있을 경우 자동으로 Mat(CvImage)으로 변환한 뒤 ExecuteCore를 호출합니다.
    /// 이를 통해 VisionPro 스텝 다음에 바로 OpenCV 스텝을 연결할 수 있습니다.
    /// </summary>
    public abstract class CvStepBase : IVisionStep
    {
        public abstract string Name { get; }
        public virtual bool ContinueOnFailure => false;

        public void Execute(VisionContext context)
        {
            // VpImage → CvImage 자동 변환 (CvImage가 없을 때만)
            if (context.MatImage == null && context.CogImage != null)
            {
                context.MatImage = ImageConverter.ToMat(context.CogImage);
                // 변환 원본 CogImage는 더 이상 필요 없으므로 해제
                (context.CogImage as IDisposable)?.Dispose();
                context.CogImage = null;
            }

            ExecuteCore(context);
        }

        protected abstract void ExecuteCore(VisionContext context);
    }
}
