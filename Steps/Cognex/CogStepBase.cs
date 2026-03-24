using Vision.Converters;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// VisionPro 기반 스텝의 추상 기반 클래스.
    ///
    /// CvImage만 있을 경우 자동으로 CogImage8Grey(VpImage)로 변환한 뒤 ExecuteCore를 호출한다.
    /// 이를 통해 OpenCV 스텝 다음에 바로 VisionPro 스텝을 연결할 수 있다.
    /// </summary>
    public abstract class CogStepBase : IVisionStep, IImageTypedStep
    {
        /// <summary>스텝 고유 이름. 하위 클래스에서 반드시 구현해야 한다.</summary>
        public abstract string Name { get; }

        private string _displayName;
        /// <summary>표시 이름. 설정하지 않으면 Name을 반환한다.</summary>
        public string DisplayName
        {
            get => string.IsNullOrEmpty(_displayName) ? Name : _displayName;
            set => _displayName = value;
        }

        /// <summary>
        /// 스텝 실패 시 파이프라인 계속 진행 여부.
        /// 기본값 false: 실패하면 이후 스텝 실행을 중단한다.
        /// </summary>
        public virtual bool ContinueOnFailure => false;

        // Cognex 스텝 기본: 이미지를 그대로 통과 (분석만)
        // 하위 클래스에서 변환이 발생하면 override
        /// <summary>요구 입력 이미지 타입. 기본값 Any (그레이/컬러 모두 허용).</summary>
        public virtual ImageType RequiredInputType  => ImageType.Any;

        /// <summary>출력 이미지 타입. 기본값 Any (이미지를 변환하지 않음).</summary>
        public virtual ImageType ProducedOutputType => ImageType.Any;

        /// <summary>
        /// 입력 이미지 키. VisionContext.Images 에서 해당 키의 이미지를 사용한다.
        /// null/비어있으면 context.CogImage (이전 스텝 출력)를 그대로 사용한다.
        /// 예: "image:-1.Green" = 원본 Green 채널,  "image:2" = 3번째 스텝 출력
        /// </summary>
        public string InputImageKey { get; set; } = null;

        /// <summary>
        /// 스텝을 실행한다. InputImageKey가 설정된 경우 해당 이미지로 교체하고,
        /// OpenCV Mat만 있는 경우 CogImage8Grey로 자동 변환 후
        /// <see cref="ExecuteCore"/>를 호출한다.
        /// </summary>
        /// <param name="context">공유 파이프라인 컨텍스트</param>
        public void Execute(VisionContext context)
        {
            // InputImageKey가 설정되어 있으면 해당 이미지를 CogImage로 교체
            if (!string.IsNullOrEmpty(InputImageKey))
                context.CogImage = context.GetInputImage(InputImageKey);

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

        /// <summary>
        /// 실제 VisionPro 처리 로직. 하위 클래스에서 구현한다.
        /// 이 메서드 호출 시점에 context.CogImage는 반드시 유효하다.
        /// </summary>
        protected abstract void ExecuteCore(VisionContext context);
    }
}
