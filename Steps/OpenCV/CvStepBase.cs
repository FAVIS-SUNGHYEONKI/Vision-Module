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
    public abstract class CvStepBase : IVisionStep, IImageTypedStep
    {
        /// <summary>스텝 고유 이름. 하위 클래스에서 반드시 구현해야 합니다.</summary>
        public abstract string Name { get; }

        /// <summary>
        /// 스텝 실패 시 파이프라인 계속 진행 여부.
        /// 기본값 false: 실패하면 이후 스텝 실행을 중단합니다.
        /// </summary>
        public virtual bool ContinueOnFailure => false;

        // OpenCV 스텝 기본: 어떤 이미지도 허용 (내부 자동 변환), 결과는 Grey
        /// <summary>요구 입력 이미지 타입. 기본값 Any (자동 변환으로 그레이/컬러 모두 허용).</summary>
        public virtual ImageType RequiredInputType  => ImageType.Any;

        /// <summary>출력 이미지 타입. OpenCV 스텝은 기본적으로 그레이스케일 Mat을 출력합니다.</summary>
        public virtual ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>
        /// 스텝을 실행합니다. CogImage만 있는 경우 OpenCV Mat으로 자동 변환 후
        /// <see cref="ExecuteCore"/>를 호출합니다.
        /// </summary>
        /// <param name="context">공유 파이프라인 컨텍스트</param>
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

        /// <summary>
        /// 실제 OpenCV 처리 로직. 하위 클래스에서 구현합니다.
        /// 이 메서드 호출 시점에 context.MatImage는 반드시 유효합니다.
        /// </summary>
        protected abstract void ExecuteCore(VisionContext context);
    }
}
