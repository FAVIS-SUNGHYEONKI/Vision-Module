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

                    try
                    {
                        await Task.Run(() => step.Execute(context), cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        context.SetError($"[{step.Name}] {ex.Message}");
                    }

                    if (!context.IsSuccess && !step.ContinueOnFailure)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                context.SetError("Pipeline cancelled.");
            }

            return context;
        }

        public void Dispose() => _steps.Clear();
    }
}
