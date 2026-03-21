# CaliperDistanceResult.cs 분석

## 역할

`CogCaliperDistanceStep`의 실행 결과를 담는 **데이터 클래스**다.

```csharp
public class CaliperDistanceResult
```

---

## 속성

| 속성 | 역할 |
|---|---|
| `X1`, `Y1` | Caliper A 에지의 이미지 좌표 (pixels) |
| `X2`, `Y2` | Caliper B 에지의 이미지 좌표 (pixels) |
| `Distance` | 두 점 간 유클리드 거리 (pixels) — 파생 계산값 |
| `CaliperId_A` | 사용한 Caliper A 인덱스 (0-based) |
| `EdgeIndex_A` | Caliper A 내 에지 인덱스 (0-based) |
| `CaliperId_B` | 사용한 Caliper B 인덱스 (0-based) |
| `EdgeIndex_B` | Caliper B 내 에지 인덱스 (0-based) |

---

## Distance — 파생 계산값

```csharp
public double Distance =>
    Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));
```

저장된 값이 아니라 `get`에서 매번 계산한다.
`X1`, `Y1`, `X2`, `Y2`가 설정되면 자동으로 올바른 거리를 반환한다.

---

## 저장 위치

```
context.Data["VisionPro.CaliperDistance.{N}"]  →  CaliperDistanceResult
```

`VisionResult.FromContext()`가 이 키를 prefix 스캔하여 `DistanceMeasurement`로 래핑한다.

---

## CaliperDistanceResult vs DistanceMeasurement 비교

| | CaliperDistanceResult | DistanceMeasurement |
|---|---|---|
| 위치 | `Vision.Steps.VisionPro` (내부) | `Vision` (공개) |
| 용도 | `context.Data`에 저장되는 원시 결과 | 외부 앱이 접근하는 타입화된 결과 |
| 접근 | 파이프라인 내부 | `result.Distances` |
