namespace Vision.UI
{
    /// <summary>
    /// 각 스텝의 파라미터를 편집하는 UserControl이 구현하는 인터페이스.
    ///
    /// 외부 폼에서 패널을 직접 사용하는 예:
    /// <code>
    /// var panel = controller.GetParamPanel(step);
    /// panel.Dock = DockStyle.Fill;
    /// myGroupBox.Controls.Add(panel);
    /// // 파라미터 변경을 스텝에 반영하려면:
    /// controller.FlushParamPanel(panel, step);
    /// </code>
    /// </summary>
    public interface IStepParamPanel
    {
        /// <summary>스텝의 현재 파라미터 값을 UI 컨트롤에 반영합니다.</summary>
        void BindStep(Vision.IVisionStep step);

        /// <summary>UI 컨트롤의 현재 값을 스텝 파라미터에 저장합니다.</summary>
        void FlushStep(Vision.IVisionStep step);
    }
}
