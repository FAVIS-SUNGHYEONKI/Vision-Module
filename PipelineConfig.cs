using System.Collections.Generic;

namespace Vision
{
    /// <summary>
    /// 하나의 파이프라인 설정을 나타내는 데이터 컨테이너.
    ///
    /// PipelineManager가 여러 PipelineConfig를 관리하며 XML로 저장/로드한다.
    /// VisionPipeline 실행 시 Steps 목록을 순서대로 AddStep()에 전달한다.
    ///
    /// XML 저장 형식: Pipeline name="..." > Steps > Step type="..." 구조
    /// </summary>
    public class PipelineConfig
    {
        /// <summary>파이프라인 표시 이름. UI ComboBox 및 XML name 속성에 사용된다.</summary>
        public string Name { get; set; } = "Pipeline";

        /// <summary>
        /// 실행 순서대로 정렬된 스텝 인스턴스 목록.
        /// VisionPipeline.AddStep()에 순서대로 전달되어 실행된다.
        /// </summary>
        public List<IVisionStep> Steps { get; set; } = new List<IVisionStep>();
    }
}
