using System;
using OpenCvSharp;

namespace Vision.Steps.OpenCV
{
    /// <summary>
    /// OpenCV Threshold 스텝.
    ///
    /// context.CvImage에 이진화를 적용하고 결과로 교체합니다.
    /// CvImage가 변경되면 VpImage는 무효가 되므로 null로 초기화합니다.
    ///
    /// 사용 예:
    ///   pipeline.AddStep(new CvThresholdStep { ThresholdValue = 128 });
    /// </summary>
    public class CvThresholdStep : CvStepBase
    {
        public override string Name => "OpenCV.Threshold";

        public double ThresholdValue { get; set; } = 128.0;
        public double MaxValue       { get; set; } = 255.0;
        public ThresholdTypes Type   { get; set; } = ThresholdTypes.Binary;

        protected override void ExecuteCore(VisionContext context)
        {
            if (context.MatImage == null || context.MatImage.Empty())
            {
                context.SetError($"{Name}: CvImage가 비어 있습니다.");
                return;
            }

            var result = new Mat();
            Cv2.Threshold(context.MatImage, result, ThresholdValue, MaxValue, Type);

            context.MatImage.Dispose();
            context.MatImage = result;
            // CvImage 교체 후 VpImage는 무효 → dispose 후 null
            (context.CogImage as IDisposable)?.Dispose();
            context.CogImage = null;
        }
    }
}
