namespace Vision.UI
{
    /// <summary>
    /// 스텝이 입력 이미지를 선택할 때 드롭다운에 표시되는 항목 하나.
    /// PipelineEditorForm이 이전 스텝들의 출력을 분석하여 목록을 구성한다.
    /// </summary>
    public class ImageSourceEntry
    {
        /// <summary>VisionContext.Images 의 키 (예: "image:-1.Green", "image:2")</summary>
        public string    Key   { get; set; }

        /// <summary>이 이미지의 타입 (Grey / Color)</summary>
        public ImageType Type  { get; set; }

        /// <summary>드롭다운에 표시할 사람이 읽기 쉬운 이름</summary>
        public string    Label { get; set; }

        public override string ToString() => Label;
    }
}
