# PipeLine.cs 분석 — VisionPipeline

## 역할

등록된 `IVisionStep`을 **순서대로 비동기 실행**하는 파이프라인 엔진이다.

```csharp
public class VisionPipeline : IDisposable
```

---

## 핵심 특징

- 메서드 체이닝으로 스텝 연결: `pipeline.AddStep(a).AddStep(b)`
- `Task.Run()`으로 각 스텝을 백그라운드 스레드에서 실행 → UI 블로킹 없음
- 스텝 예외는 `context.SetError()`로 변환 → 파이프라인 전체를 중단시키지 않음
- `CancellationToken` 지원

---

## AddStep

```csharp
public VisionPipeline AddStep(IVisionStep step)
{
    _steps.Add(step);
    return this;    // 체이닝을 위해 자기 자신 반환
}
```

`return this`로 체이닝이 가능하다:
```csharp
pipeline.AddStep(convertGray).AddStep(caliper).AddStep(blob);
```

---

## RunAsync — 실행 흐름

```csharp
public async Task<VisionContext> RunAsync(
    VisionContext context,
    CancellationToken cancellationToken = default)
```

```
for (각 스텝)
 ├─ cancellationToken.ThrowIfCancellationRequested()
 │
 ├─ try
 │   └─ await Task.Run(() => step.Execute(context))
 │
 ├─ catch (Exception) when (OperationCanceledException 제외)
 │   └─ context.SetError($"[{step.Name}] {ex.Message}")
 │
 └─ if (!context.IsSuccess && !step.ContinueOnFailure)
     └─ break   ← 파이프라인 즉시 중단

catch (OperationCanceledException)
 └─ context.SetError("Pipeline cancelled.")
```

### 예외 처리 설계

`when (!(ex is OperationCanceledException))` 필터로 취소 예외를 분리한다.
- 일반 예외 → `SetError()` 기록 후 다음 스텝 판단
- 취소 예외 → 외부 catch로 전달 → "cancelled" 기록 후 종료

### ContinueOnFailure 동작

| `IsSuccess` | `ContinueOnFailure` | 결과 |
|---|---|---|
| true | 무관 | 다음 스텝 실행 |
| false | true | 다음 스텝 실행 |
| false | false | **파이프라인 중단** |

---

## Dispose

```csharp
public void Dispose() => _steps.Clear();
```

스텝 목록만 비운다. **스텝 인스턴스 자체는 해제하지 않는다.**
스텝은 `PipelineConfig.Steps`가 소유하기 때문이다.

---

## PipelineConfig와의 관계

```
PipelineConfig (데이터)          VisionPipeline (실행 엔진)
 └─ List<IVisionStep> Steps  →  AddStep() 순서대로 등록 → RunAsync()
```

`PipelineConfig`는 영구 보관, `VisionPipeline`은 실행 시 임시 생성 후 폐기한다.
(`PipelineController.RunAsync()`에서 매 실행마다 `new VisionPipeline()`을 생성한다.)
