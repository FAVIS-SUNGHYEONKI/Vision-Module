using System;
using Cognex.VisionPro;
using Vision.Converters;

namespace Vision.Steps.OpenCV
{
    /// <summary>
    /// OpenCV 기반 스텝의 추상 기반 클래스.
    ///
    /// VpImage만 있을 경우 자동으로 Mat(CvImage)으로 변환한 뒤 ExecuteCore를 호출한다.
    /// 이를 통해 VisionPro 스텝 다음에 바로 OpenCV 스텝을 연결할 수 있다.
    /// </summary>
    public abstract class CvStepBase : IVisionStep, IImageTypedStep
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

        // OpenCV 스텝 기본: 어떤 이미지도 허용 (내부 자동 변환), 결과는 Grey
        /// <summary>요구 입력 이미지 타입. 기본값 Any (자동 변환으로 그레이/컬러 모두 허용).</summary>
        public virtual ImageType RequiredInputType  => ImageType.Any;

        /// <summary>출력 이미지 타입. OpenCV 스텝은 기본적으로 그레이스케일 Mat을 출력한다.</summary>
        public virtual ImageType ProducedOutputType => ImageType.Grey;

        /// <summary>
        /// 입력 이미지 키. VisionContext.Images 에서 해당 키의 이미지를 사용한다.
        /// null/비어있으면 context.CogImage (이전 스텝 출력)를 그대로 사용한다.
        /// 예: "image:-1.Green" = 원본 Green 채널,  "image:2" = 3번째 스텝 출력
        /// </summary>
        public string InputImageKey { get; set; } = null;

        /// <summary>
        /// 스텝을 실행한다. InputImageKey가 설정된 경우 해당 이미지로 교체하고,
        /// CogImage만 있는 경우 OpenCV Mat으로 자동 변환 후
        /// <see cref="ExecuteCore"/>를 호출한다.
        /// </summary>
        /// <param name="context">공유 파이프라인 컨텍스트</param>
        public void Execute(VisionContext context)
        {
            // InputImageKey가 설정되어 있으면 해당 이미지를 CogImage로 교체
            if (!string.IsNullOrEmpty(InputImageKey))
            {
                context.MatImage?.Dispose();
                context.MatImage = null;
                context.CogImage = context.GetInputImage(InputImageKey);
            }

            // VpImage → CvImage 자동 변환 (CvImage가 없을 때만)
            if (context.MatImage == null && context.CogImage != null)
            {
                context.MatImage = ImageConverter.ToMat(context.CogImage);
                // 변환 원본 CogImage는 더 이상 필요 없으므로 해제
                (context.CogImage as IDisposable)?.Dispose();
                context.CogImage = null;
            }

            ExecuteCore(context);

            // 출력 이미지를 이름 저장소에 등록 (Mat → CogImage 변환 후)
            if (context.IsSuccess && context.MatImage != null && !context.MatImage.Empty())
            {
                var cogOut = ImageConverter.ToCogImage8Grey(context.MatImage);
                context.RegisterImage("image:" + context.CurrentStepIndex, cogOut);
                // context.CogImage도 갱신 (다음 CogStep이 자동 변환 없이 사용 가능)
                if (context.CogImage == null)
                    context.CogImage = cogOut;
            }
        }

        /// <summary>
        /// 실제 OpenCV 처리 로직. 하위 클래스에서 구현한다.
        /// 이 메서드 호출 시점에 context.MatImage는 반드시 유효하다.
        /// </summary>
        protected abstract void ExecuteCore(VisionContext context);
    }
}
