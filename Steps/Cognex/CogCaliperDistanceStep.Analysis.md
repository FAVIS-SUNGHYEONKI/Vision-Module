# CogCaliperDistanceStep.cs 분석

## 역할

파이프라인 내 두 `CogCaliperStep`의 에지 결과 간 **유클리드 거리를 계산**하는 스텝이다.

```csharp
public class CogCaliperDistanceStep : IVisionStep, IImageTypedStep, IStepSerializable, IInspectionStep
```

---

## CogStepBase를 상속하지 않는 이유

이미지를 처리하지 않고 `context.Data`만 읽는다.
`CogStepBase`의 Mat→Cog 자동 변환이 불필요하므로 직접 `IVisionStep`을 구현했다.

---

## 파라미터

```csharp
public int CaliperId_A { get; set; } = 0;   // 참조할 Caliper 결과 인덱스 A
public int EdgeIndex_A { get; set; } = 0;   // Caliper A 내 에지 인덱스
public int CaliperId_B { get; set; } = 1;   // 참조할 Caliper 결과 인덱스 B
public int EdgeIndex_B { get; set; } = 0;   // Caliper B 내 에지 인덱스
```

기본값: 파이프라인의 첫 번째 Caliper(0)와 두 번째 Caliper(1)의 첫 번째 에지(0) 거리 계산.

---

## Execute — 실행 흐름

```
1. context.Data["VisionPro.Caliper.{CaliperId_A}"] 조회
   → 없으면 SetError() + return

2. context.Data["VisionPro.Caliper.{CaliperId_B}"] 조회
   → 없으면 SetError() + return

3. 각 에지 인덱스 유효성 확인

4. Region 조회 (optional)
   "VisionPro.Caliper.{N}.Region" → CogRectangleAffine

5. 1D Position → 2D (X, Y) 변환
   ToImageXY(position, region, out x, out y)

6. 결과 저장
   context.Data["VisionPro.CaliperDistance.{N}"] = CaliperDistanceResult
```

---

## ToImageXY — 좌표 변환

```csharp
private static void ToImageXY(
    double position, CogRectangleAffine region,
    out double x, out double y)
{
    if (region != null)
    {
        x = region.CenterX + Math.Cos(region.Rotation) * position;
        y = region.CenterY + Math.Sin(region.Rotation) * position;
    }
    else
    {
        x = position;
        y = 0;
    }
}
```

Caliper의 `Position`은 **스캔 방향의 1D 거리**다.
Region의 중심점과 회전각을 이용해 이미지 2D 좌표로 변환한다.

```
Region 중심 (CenterX, CenterY)
  + cos(Rotation) * Position  →  X
  + sin(Rotation) * Position  →  Y
```

---

## 전제 조건

이 스텝이 실행되기 전에 참조된 `CogCaliperStep`들이 먼저 실행되어야 한다.
파이프라인 순서:
```
[CogCaliperStep]  → context.Data["VisionPro.Caliper.0"]
[CogCaliperStep]  → context.Data["VisionPro.Caliper.1"]
[CogCaliperDistanceStep]  → 위 두 결과를 읽어 거리 계산
```

---

## XML 직렬화 필드

`CaliperId_A`, `EdgeIndex_A`, `CaliperId_B`, `EdgeIndex_B` 4개만 저장한다.
