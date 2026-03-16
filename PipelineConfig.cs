using System.Collections.Generic;

namespace Vision
{
    /// <summary>
    /// 하나의 파이프라인 이름과 스텝 목록을 담는 설정 컨테이너.
    /// </summary>
    public class PipelineConfig
    {
        public string            Name  { get; set; } = "Pipeline";
        public List<IVisionStep> Steps { get; set; } = new List<IVisionStep>();
    }
}
