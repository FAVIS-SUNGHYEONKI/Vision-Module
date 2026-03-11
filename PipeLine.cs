using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Vision
{
    /// <summary>
    /// 여러 IVisionStep을 순차적으로 실행하는 파이프라인.
    /// 스텝별 예외를 격리하고, ContinueOnFailure 플래그로 실패 시 계속/중단을 제어합니다.
    /// </summary>
    public class VisionPipeline : IDisposable
    {
        private readonly List<IVisionStep> _steps = new List<IVisionStep>();

        /// <summary>
        /// true이면 파이프라인 완료 후 VisionContext의 이미지(MatImage/CogImage)를 자동으로 해제합니다.
        /// Data, Errors 등 결과 데이터는 유지됩니다.
        /// </summary>
        public bool AutoDisposeImages { get; set; } = false;

        /// <summary>스텝 시작 시 발생. (스텝 이름, 현재 번호, 전체 수)</summary>
        public event Action<string, int, int> OnStepStarted;

        /// <summary>스텝에서 예외 발생 시 전달. (스텝 이름, 예외)</summary>
        public event Action<string, Exception> OnStepFailed;

        /// <summary>파이프라인 완료(성공·실패·취소) 시 발생.</summary>
        public event Action<VisionContext> OnPipelineFinished;

        // 메서드 체이닝 .AddStep().AddStep()...
        public VisionPipeline AddStep(IVisionStep step)
        {
            _steps.Add(step);
            return this;
        }

        /// <summary>파이프라인을 비동기로 실행하고 최종 VisionContext를 반환합니다.</summary>
        public async Task<VisionContext> RunAsync(
            VisionContext context,
            CancellationToken cancellationToken = default)
        {
            try
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var step = _steps[i];
                    OnStepStarted?.Invoke(step.Name, i + 1, _steps.Count);

                    try
                    {
                        await Task.Run(() => step.Execute(context), cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        context.SetError($"[{step.Name}] {ex.Message}");
                        OnStepFailed?.Invoke(step.Name, ex);
                    }

                    // 실패 시 ContinueOnFailure가 false면 즉시 중단
                    if (!context.IsSuccess && !step.ContinueOnFailure)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                context.SetError("Pipeline cancelled.");
            }
            finally
            {
                OnPipelineFinished?.Invoke(context);
                if (AutoDisposeImages)
                    context.Dispose();
            }

            return context;
        }

        public void Dispose() => _steps.Clear();
    }
}
