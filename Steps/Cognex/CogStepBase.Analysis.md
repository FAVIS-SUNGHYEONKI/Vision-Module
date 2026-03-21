# CogStepBase.cs 분석

## 역할

VisionPro 기반 스텝의 **추상 기반 클래스**다.
`MatImage`만 있을 때 자동으로 `CogImage8Grey`로 변환하여 하위 클래스가 항상 CogImage를 받도록 보장한다.

```csharp
public abstract class CogStepBase : IVisionStep, IImageTypedStep
```

---

## 자동 변환 로직 — Execute

```csharp
public void Execute(VisionContext context)
{
    if (context.CogImage == null && context.MatImage != null)
    {
        context.CogImage = ImageConverter.ToCogImage8Grey(context.MatImage);
        context.MatImage.Dispose();
        context.MatImage = null;
    }

    ExecuteCore(context);
}
```

- `CogImage`가 이미 있으면 변환 없이 바로 `ExecuteCore()` 호출
- `MatImage`만 있으면 변환 후 원본 Mat 즉시 해제
- `ExecuteCore()` 호출 시점에 `context.CogImage`는 반드시 유효하다.

---

## 추상 멤버

```csharp
public abstract string Name { get; }
protected abstract void ExecuteCore(VisionContext context);
```

하위 클래스에서 반드시 구현해야 한다.

---

## 기본값

| 속성 | 기본값 | 의미 |
|---|---|---|
| `ContinueOnFailure` | `false` | 실패 시 파이프라인 중단 |
| `RequiredInputType` | `Any` | 그레이/컬러 모두 허용 |
| `ProducedOutputType` | `Any` | 이미지를 변환하지 않음 |

하위 클래스에서 `override`로 재정의 가능하다.

---

## CogStepBase vs CvStepBase 비교

| | CogStepBase | CvStepBase |
|---|---|---|
| 자동 변환 방향 | Mat → CogImage | CogImage → Mat |
| 기본 출력 타입 | Any | Grey |
| ExecuteCore 보장 | `context.CogImage` 유효 | `context.MatImage` 유효 |

---

## 하위 클래스 목록

| 클래스 | 기능 |
|---|---|
| `CogCaliperStep` | 에지 검출 |
| `CogBlobStep` | 영역 검출 |
| `CogConvertGray` | 컬러 → 그레이 채널 추출 |
| `CogWeightedRGBStep` | 가중 RGB 합산 → 그레이 |
