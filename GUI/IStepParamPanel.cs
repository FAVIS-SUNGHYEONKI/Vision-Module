namespace Vision.UI
{
    /// <summary>
    /// 각 스텝의 파라미터를 편집하는 UserControl이 구현하는 인터페이스.
    ///
    /// PipelineEditorForm 내부와 외부 앱 모두에서 동일하게 사용한다.
    ///
    /// 외부 앱 사용 패턴:
    ///   var ctrl  = controller.GetParamPanel(step);
    ///   var panel = (IStepParamPanel)ctrl;
    ///   ctrl.Dock = DockStyle.Fill;
    ///   myGroupBox.Controls.Add(ctrl);
    ///   // 저장 버튼 클릭 시:
    ///   controller.ApplyAndSave(step, panel);
    /// </summary>
    public interface IStepParamPanel
    {
        /// <summary>스텝의 현재 파라미터 값을 UI 컨트롤에 반영한다.</summary>
        void BindStep(Vision.IVisionStep step);

        /// <summary>UI 컨트롤의 현재 값을 스텝 파라미터에 기록한다.</summary>
        void FlushStep(Vision.IVisionStep step);
    }

    /// <summary>
    /// 입력 이미지 선택 기능을 지원하는 파라미터 패널이 추가로 구현하는 인터페이스.
    /// PipelineEditorForm이 스텝 선택 시 호출하여 선택 가능한 이미지 목록을 전달한다.
    /// </summary>
    public interface IInputImageSelectable
    {
        /// <summary>선택 가능한 입력 이미지 목록을 패널에 전달한다. BindStep 이후 호출된다.</summary>
        void SetAvailableInputImages(System.Collections.Generic.IReadOnlyList<ImageSourceEntry> images);
    }
}
