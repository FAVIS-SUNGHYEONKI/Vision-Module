# CogWeightedRGBStep.cs 분석

## 역할

R / G / B 채널에 각각 가중치를 곱해 합산하는 **고급 컬러 → 그레이 변환** 스텝이다.

```csharp
public class CogWeightedRGBStep : CogStepBase, IStepSerializable
```

계산식: `result = clip(R * RedWeight + G * GreenWeight + B * BlueWeight, 0, 255)`

---

## 가중치 속성

```csharp
public double RedWeight   { get; set; } = 1.0 / 3.0;
public double GreenWeight { get; set; } = 1.0 / 3.0;
public double BlueWeight  { get; set; } = 1.0 / 3.0;
```

기본값: 균등 합산 (각 1/3).
BT.601 표준 휘도: `Red=0.299, Green=0.587, Blue=0.114`

---

## ExecuteCore — 처리 순서

```
1. CogImage24PlanarColor 타입 확인
2. GetPlane(0/1/2) → R, G, B 채널 추출
3. ApplyWeight(planeR, RedWeight)   → weightedR
   ApplyWeight(planeG, GreenWeight) → weightedG
   ApplyWeight(planeB, BlueWeight)  → weightedB
4. _addRG:    weightedR + weightedG → 중간 합
5. _addFinal: 중간 합 + weightedB  → 최종 그레이
6. context.CogImage = 최종 그레이 이미지
```

`CogImageArithmeticTool`(Add) 2개를 생성자에서 미리 초기화한다:
```csharp
_addRG.RunParams.Operator    = CogImageArithmeticConstants.Add;
_addFinal.RunParams.Operator = CogImageArithmeticConstants.Add;
```

---

## ApplyWeight — 픽셀 직접 연산

```csharp
private static CogImage8Grey ApplyWeight(CogImage8Grey src, double weight)
```

`Get8GreyPixelMemory`로 직접 픽셀 접근 후 `Marshal.Copy`로 가중치를 적용한다.
`ImageConverter`와 동일한 직접 메모리 접근 패턴이다.

```csharp
dstRow[x] = val >= 255.0 ? (byte)255 :
             val <= 0.0   ? (byte)0   : (byte)val;
```

0~255 범위로 클램핑한다.

---

## 메모리 해제

```csharp
finally
{
    (srcMem as IDisposable)?.Dispose();
    (dstMem as IDisposable)?.Dispose();
}
```

`ICogImage8PixelMemory`는 `IDisposable`을 직접 구현하지 않으므로 캐스팅 후 해제한다.

---

## CogConvertGray와 비교

| | CogConvertGray | CogWeightedRGBStep |
|---|---|---|
| 방식 | 단일 채널 추출 | 가중 합산 |
| 연산 비용 | 낮음 | 높음 (픽셀 순회 + 두 번 Add) |
| BT.601 표준 | 불가 | 가능 |
| 적합한 상황 | 단색 조명 | 일반적인 컬러 → 그레이 |
