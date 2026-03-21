using System.Windows.Forms;

namespace Vision.UI
{
    /// <summary>
    /// 스텝 타입에 맞는 파라미터 편집 패널(UserControl)을 생성하는 팩토리.
    /// 새 스텝을 추가할 때 여기에 case 하나만 추가하면 됩니다.
    /// </summary>
    public static class StepParamPanelFactory
    {
        /// <summary>
        /// 스텝에 맞는 IStepParamPanel(UserControl)을 반환합니다.
        /// 해당하는 패널이 없으면 null을 반환합니다.
        /// </summary>
        public static Control Create(IVisionStep step)
        {
            switch (step?.Name)
            {
                case "VisionPro.Caliper":         return new CogCaliperParamPanel();
                case "VisionPro.Blob":            return new CogBlobParamPanel();
                case "VisionPro.ConvertGray":     return new CogConvertGrayParamPanel();
                case "VisionPro.CaliperDistance": return new CogCaliperDistanceParamPanel();
                case "OpenCV.Threshold":          return new CvThresholdParamPanel();
                default:                          return null;
            }
        }
    }
}
