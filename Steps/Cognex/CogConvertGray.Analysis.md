# CogConvertGray.cs 분석

## 역할

`CogImage24PlanarColor`에서 R / G / B 단일 채널을 추출하여 **그레이스케일로 변환**하는 스텝이다.

```csharp
public class CogConvertGray : CogStepBase, IStepSerializable
```

---

## 이미지 타입

| | 값 |
|---|---|
| `RequiredInputType` | `ImageType.Color` |
| `ProducedOutputType` | `ImageType.Grey` |

컬러 이미지만 입력으로 받는다. 그레이 이미지를 넣으면 오류가 발생한다.

---

## 채널 선택

```csharp
public enum ColorChannel { Red = 0, Green = 1, Blue = 2 }
public ColorChannel Channel { get; set; } = ColorChannel.Green;
```

기본값은 Green이다.

`CogImage24PlanarColor.GetPlane(index)`를 사용한다:
- `GetPlane(0)` → Red 채널
- `GetPlane(1)` → Green 채널
- `GetPlane(2)` → Blue 채널

---

## ExecuteCore

```csharp
protected override void ExecuteCore(VisionContext context)
{
    var colorImg = context.CogImage as CogImage24PlanarColor;
    if (colorImg == null)
    {
        context.SetError(...);
        return;
    }

    var plane = colorImg.GetPlane((CogImagePlaneConstants)Channel);
    context.CogImage = plane;
    context.MatImage = null;
}
```

추출한 채널 이미지로 `context.CogImage`를 교체한다.
`context.MatImage`를 null로 설정하여 이전 Mat이 남아있지 않게 한다.

---

## CogConvertGray vs CogWeightedRGBStep 비교

| | CogConvertGray | CogWeightedRGBStep |
|---|---|---|
| 방식 | 단일 채널 추출 | R+G+B 가중 합산 |
| 활용 | 특정 채널 강조 | 표준 휘도 변환 |
| 계산 비용 | 낮음 | 높음 |
| BT.601 표준 | 불가 | 가능 (R=0.299, G=0.587, B=0.114) |

---

## 활용 예

- 적색 조명 환경 → Red 채널 선택 (대비 최대)
- 표준 그레이 변환 → `CogWeightedRGBStep` 권장
