# CvStepBase.cs 분석

## 역할

OpenCV 기반 스텝의 **추상 기반 클래스**다.
`CogImage`만 있을 때 자동으로 `Mat`으로 변환하여 하위 클래스가 항상 MatImage를 받도록 보장한다.

```csharp
public abstract class CvStepBase : IVisionStep, IImageTypedStep
```

---

## 자동 변환 로직 — Execute

```csharp
public void Execute(VisionContext context)
{
    if (context.MatImage == null && context.CogImage != null)
    {
        context.MatImage = ImageConverter.ToMat(context.CogImage);
        (context.CogImage as IDisposable)?.Dispose();
        context.CogImage = null;
    }

    ExecuteCore(context);
}
```

- `MatImage`가 이미 있으면 변환 없이 바로 `ExecuteCore()` 호출
- `CogImage`만 있으면 변환 후 원본 CogImage 즉시 해제
- `ExecuteCore()` 호출 시점에 `context.MatImage`는 반드시 유효하다.

`ICogImage`는 `IDisposable`을 직접 구현하지 않으므로 캐스팅 후 해제한다.

---

## 추상 멤버

```csharp
public abstract string Name { get; }
protected abstract void ExecuteCore(VisionContext context);
```

---

## 기본값

| 속성 | 기본값 | 의미 |
|---|---|---|
| `ContinueOnFailure` | `false` | 실패 시 파이프라인 중단 |
| `RequiredInputType` | `Any` | 자동 변환으로 모두 허용 |
| `ProducedOutputType` | **`Grey`** | OpenCV 스텝은 기본적으로 그레이 Mat 출력 |

`CogStepBase`와 달리 `ProducedOutputType`의 기본값이 `Grey`다.
OpenCV 스텝들은 대부분 그레이스케일 Mat을 결과로 남기기 때문이다.

---

## CogStepBase와 대칭 구조

```
CogStepBase.Execute:
  MatImage → ToCogImage8Grey → CogImage  →  ExecuteCore

CvStepBase.Execute:
  CogImage → ToMat            → MatImage →  ExecuteCore
```

두 클래스는 변환 방향만 반대이고 나머지 구조는 동일하다.

---

## 하위 클래스 목록

| 클래스 | 기능 |
|---|---|
| `CvThresholdStep` | 이진화 (Threshold) |
