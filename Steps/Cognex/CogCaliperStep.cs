using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// VisionPro CogCaliperTool을 IVisionStep으로 래핑한 스텝.
    ///
    /// 사용 예:
    ///   pipeline.AddStep(new VpCaliperStep { ExpectedLineWidth = 10.0 });
    /// 결과는 context.Data["VisionPro.Caliper"] 에 CogCaliperResultCollection으로 저장됩니다.
    /// </summary>
    public class CogCaliperStep : CogStepBase
    {
        private readonly CogCaliperTool _tool = new CogCaliperTool();

        public override string Name => "VisionPro.Caliper";
        /// <summary>
        /// CogCaliperTool의 실행 파라미터.
        /// ContrastThreshold, NumberToFind 등을 직접 수정합니다.
        /// </summary>
        public CogCaliper RunParams => _tool.RunParams;

        /// <summary>
        /// 검사 영역. CogRectangleAffine
        /// </summary>
        public CogRectangleAffine Region
        {
            get => _tool.Region;
            set => _tool.Region = value;
        }

        protected override void ExecuteCore(VisionContext context)
        {
            _tool.InputImage = context.CogImage;

            _tool.Run();

            if (_tool.Results != null && _tool.Results.Count > 0)
                context.Data[Name] = _tool.Results;
            else
                context.SetError($"{Name}: Caliper 결과 없음.");
        }
    }
}
