using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cognex.VisionPro;


namespace Vision
{
    /// <summary>
    /// 등록된 IVisionStep을 순서대로 실행하는 파이프라인 실행 엔진.
    ///
    /// 특징:
    ///   - 메서드 체이닝으로 스텝을 연결한다: pipeline.AddStep(step1).AddStep(step2)
    ///   - 각 스텝은 Task.Run()으로 백그라운드 스레드에서 실행된다.
    ///   - 스텝 내부 예외는 개별 try-catch로 격리되어 파이프라인 전체를 중단시키지 않는다.
    ///     단, 해당 스텝의 ContinueOnFailure = false 이면 그 스텝 이후는 실행되지 않는다.
    ///   - CancellationToken을 지원하여 외부에서 실행을 취소할 수 있다.
    ///
    /// 사용 예:
    ///   using var pipeline = new VisionPipeline();
    ///   pipeline.AddStep(convertGrayStep).AddStep(caliperStep);
    ///   var result = await pipeline.RunAsync(context);
    /// </summary>
    public class VisionPipeline : IDisposable
    {
        /// <summary>등록된 스텝 목록. AddStep() 호출 순서로 실행된다.</summary>
        private readonly List<IVisionStep> _steps = new List<IVisionStep>();

        /// <summary>
        /// 스텝을 파이프라인 끝에 추가한다.
        /// </summary>
        /// <param name="step">추가할 스텝 인스턴스</param>
        /// <returns>메서드 체이닝을 위해 자기 자신(this)을 반환한다.</returns>
        public VisionPipeline AddStep(IVisionStep step)
        {
            _steps.Add(step);
            return this;
        }

        /// <summary>
        /// 등록된 모든 스텝을 순서대로 비동기 실행하고 완료된 컨텍스트를 반환한다.
        ///
        /// 실행 흐름:
        ///   1. 각 스텝을 Task.Run()으로 백그라운드에서 실행
        ///   2. 스텝 예외 발생 → context.SetError() 기록, 파이프라인은 계속 진행 시도
        ///   3. context.IsSuccess = false 이고 step.ContinueOnFailure = false 이면 즉시 중단
        ///   4. CancellationToken 요청 시 OperationCanceledException을 잡아 오류 기록 후 종료
        /// </summary>
        /// <param name="context">스텝들이 공유하는 이미지 및 결과 컨텍스트</param>
        /// <param name="cancellationToken">외부 취소 토큰 (기본값: 취소 없음)</param>
        /// <returns>처리 완료된 VisionContext (입력과 동일한 인스턴스)</returns>
        public async Task<VisionContext> RunAsync(
            VisionContext context,
            CancellationToken cancellationToken = default)
        {
            // 컬러 이미지로 시작하는 경우 원본을 보존 (채널별 Grey 캐시 소스)
            if (context.OriginalColorImage == null && context.CogImage is CogImage24PlanarColor colorImg)
                context.OriginalColorImage = colorImg;

            // 원본 이미지를 이름 저장소에 등록 (Color이면 R/G/B 채널도 자동 등록)
            context.RegisterImage("image:-1", context.CogImage);

            try
            {
                for (int i = 0; i < _steps.Count; i++)
                {
                    // 취소 요청이 있으면 즉시 OperationCanceledException 발생
                    cancellationToken.ThrowIfCancellationRequested();

                    var step = _steps[i];
                    context.CurrentStepIndex = i;

                    try
                    {
                        // 각 스텝을 별도 스레드에서 실행 (UI 블로킹 방지)
                        await Task.Run(() => step.Execute(context), cancellationToken);
                    }
                    catch (Exception ex) when (!(ex is OperationCanceledException))
                    {
                        // 스텝 내부 예외를 컨텍스트 오류로 변환 (파이프라인은 계속 진행 판단)
                        context.SetError($"[{step.Name}] {ex.Message}");
                    }

                    // 실패 상태이고 이 스텝이 중단을 요구하면 루프 탈출
                    if (!context.IsSuccess && !step.ContinueOnFailure)
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // 취소 요청을 오류로 기록하고 정상 종료
                context.SetError("Pipeline cancelled.");
            }

            return context;
        }

        /// <summary>
        /// 파이프라인을 폐기한다. 등록된 스텝 목록을 비운다.
        /// 스텝 인스턴스 자체는 파이프라인이 소유하지 않으므로 여기서 해제하지 않는다.
        /// </summary>
        public void Dispose() => _steps.Clear();
    }
}
