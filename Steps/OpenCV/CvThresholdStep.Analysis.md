# CvThresholdStep.cs 분석

## 역할

OpenCV `Cv2.Threshold()`를 사용하여 **그레이스케일 이미지를 이진화**하는 스텝이다.

```csharp
public class CvThresholdStep : CvStepBase, IStepSerializable
```

`CvStepBase` 상속 → CogImage가 있으면 자동으로 Mat으로 변환 후 실행된다.

---

## 파라미터

| 속성 | 기본값 | 역할 |
|---|---|---|
| `ThresholdValue` | `128.0` | 이진화 임계값 (0~255). Otsu/Triangle 타입에서는 무시 |
| `MaxValue` | `255.0` | 임계값 초과 픽셀에 설정할 최대값 |
| `Type` | `Binary` | 이진화 방식 |

---

## ThresholdTypes 주요 값

| 타입 | 동작 |
|---|---|
| `Binary` | pixel > thresh → MaxValue, 나머지 → 0 |
| `BinaryInv` | pixel > thresh → 0, 나머지 → MaxValue |
| `Otsu` | 자동 최적 임계값 계산 (ThresholdValue 무시) |
| `Triangle` | 삼각형 알고리즘 자동 임계값 |

---

## ExecuteCore

```csharp
protected override void ExecuteCore(VisionContext context)
{
    var result = new Mat();
    Cv2.Threshold(context.MatImage, result, ThresholdValue, MaxValue, Type);

    context.MatImage.Dispose();      // 원본 Mat 해제
    context.MatImage = result;       // 결과로 교체
    (context.CogImage as IDisposable)?.Dispose();
    context.CogImage = null;         // CogImage 무효화
}
```

실행 후:
- `context.MatImage` = 이진화된 Mat
- `context.CogImage` = null

다음 스텝이 CogImage를 필요로 하면 `CogStepBase`가 자동으로 Mat → Cog로 변환한다.

---

## XML 직렬화 필드

`ThresholdValue`, `MaxValue`, `Type` (int로 저장)

```xml
<Step type="OpenCV.Threshold">
  <ThresholdValue>128</ThresholdValue>
  <MaxValue>255</MaxValue>
  <Type>0</Type>
</Step>
```
