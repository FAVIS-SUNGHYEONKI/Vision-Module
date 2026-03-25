using System.Collections.Generic;
using Vision.Steps.VisionPro;
using Vision.Steps.OpenCV;
using System;
using Cognex.VisionPro;

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
        /// 이 파이프라인에 배정된 입력 이미지.
        /// RunAsync / RunStep / ResolveInputImage 등 모든 실행 시 기준 이미지로 사용된다.
        /// </summary>
        public ICogImage InputImage { get; private set; }

        /// <summary>
        /// 이 파이프라인의 입력 이미지를 설정한다.
        ///
        /// 사용 예: controller.Pipelines[0].SetInputImage(image0);
        /// </summary>
        public void SetInputImage(ICogImage image) { InputImage = image; }

        /// <summary>PipelineManager가 주입하는 저장 콜백. 외부에서 직접 설정하지 않는다.</summary>
        internal Action SaveCallback { get; set; }

        /// <summary>
        /// 이 파이프라인 설정을 파일에 저장한다.
        ///
        /// 사용 예: controller.Pipelines[0].Save();
        /// </summary>
        public void Save() => SaveCallback?.Invoke();

        /// <summary>
        /// 지정한 스텝의 변경사항을 파이프라인 파일에 저장한다.
        ///
        /// 파이프라인 파일은 모든 스텝을 함께 저장하므로 내부적으로 Save()와 동일하게 동작한다.
        /// 호출 의도("이 스텝의 설정을 저장")를 명확히 표현할 때 사용한다.
        ///
        /// 사용 예:
        ///   controller.ApplyParamPanel(step, panel);
        ///   controller.Pipelines[0].SaveStep(step);
        /// </summary>
        public void SaveStep(IVisionStep step) => SaveCallback?.Invoke();

        /// <summary>
        /// 실행 순서대로 정렬된 스텝 인스턴스 목록.
        /// VisionPipeline.AddStep()에 순서대로 전달되어 실행된다.
        /// </summary>
        public List<IVisionStep> Steps { get; set; } = new List<IVisionStep>();

        /// <summary>
        /// 타입 T에 해당하는 스텝 전체를 순서대로 반환한다.
        ///
        /// 사용 예: var calipers = controller.Pipelines[0].GetSteps&lt;CogCaliperStep&gt;();
        /// </summary>
        public IReadOnlyList<T> GetSteps<T>() where T : class, IVisionStep
        {
            var result = new List<T>();
            foreach (var step in Steps)
            {
                var typed = step as T;
                if (typed != null) result.Add(typed);
            }
            return result;
        }

        /// <summary>
        /// 타입 T에 해당하는 N번째(0-based) 스텝을 반환한다.
        ///
        /// 사용 예: var caliper = controller.Pipelines[0].GetStep&lt;CogCaliperStep&gt;(0);
        /// </summary>
        public T GetStep<T>(int stepIndex) where T : class, IVisionStep
        {
            int count = 0;
            foreach (var step in Steps)
            {
                var typed = step as T;
                if (typed == null) continue;
                if (count++ == stepIndex) return typed;
            }
            return null;
        }

        /// <summary>
        /// 스텝의 입력 이미지 키를 설정한다.
        ///
        /// GetAvailableInputImages()가 반환한 목록의 Key 값을 그대로 전달한다.
        /// null 또는 빈 문자열을 전달하면 원본 이미지(image:-1)를 사용한다.
        ///
        /// 사용 예:
        ///   controller.Pipelines[0].SetStepInputImageKey(step, key);
        ///   controller.Pipelines[0].Save();
        /// </summary>
        /// <param name="step">키를 설정할 스텝.</param>
        /// <param name="key">ImageSourceEntry.Key 값 (예: "image:-1", "image:0", "image:-1.Red").</param>
        public void SetStepInputImageKey(IVisionStep step, string key)
        {
            var cogStep = step as CogStepBase;
            if (cogStep != null) { cogStep.InputImageKey = key; return; }
            var cvStep = step as CvStepBase;
            if (cvStep != null) cvStep.InputImageKey = key;
        }
    }
}
