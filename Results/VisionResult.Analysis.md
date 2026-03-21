# VisionResult 클래스 분석

## 전체 구성

파일 하나에 **4개의 클래스**가 정의되어 있다.

```
VisionResult              — 파이프라인 실행 결과 컨테이너 (메인)
 ├─ CaliperEdge           — Caliper 에지 하나의 결과
 ├─ BlobItem              — Blob 하나의 결과
 └─ DistanceMeasurement   — 두 에지 간 거리 측정 결과
```

---

## VisionResult — 메인 컨테이너

### 불변 객체 설계

```csharp
public sealed class VisionResult
```

`sealed` = 상속 불가. 생성자가 `private`이므로 외부에서 직접 `new`로 만들 수 없다.
생성 경로는 두 개뿐이다.

| 생성 방법 | 용도 |
|---|---|
| `VisionResult.Empty` | 파이프라인 미구성 등 즉시 실패 시 |
| `VisionResult.FromContext(context, steps)` | 파이프라인 실행 후 정상 변환 시 |

한 번 만들어진 결과는 수정이 불가능하다. 모든 속성이 `{ get; }` (setter 없음)이다.

---

### 속성 구성

```csharp
public bool IsSuccess { get; }
public IReadOnlyList<string> Errors { get; }
public IReadOnlyList<CaliperEdge> CaliperEdges { get; }
public IReadOnlyList<BlobItem> Blobs { get; }
public IReadOnlyList<DistanceMeasurement> Distances { get; }
```

`IReadOnlyList`를 쓰는 이유: 외부에서 `Add` / `Remove` 등 리스트 조작을 막기 위함이다.
내부에서는 `List<T>`로 만들어서 채운 뒤 그대로 넘긴다.
(C#에서 `List<T>`는 `IReadOnlyList<T>`를 구현한다.)

---

### 필터 메서드

```csharp
public IEnumerable<CaliperEdge> GetEdges(int caliperId)
    => CaliperEdges.Where(e => e.CaliperId == caliperId);

public IEnumerable<BlobItem> GetBlobs(int blobStepId)
    => Blobs.Where(b => b.BlobStepId == blobStepId);
```

스텝이 여러 개인 파이프라인에서 **특정 스텝의 결과만 꺼낼 때** 사용한다.

```csharp
// 예: 파이프라인에 Caliper 스텝이 3개일 때
var edgesFromStep2 = result.GetEdges(1);  // 0-based, 두 번째 Caliper 스텝 결과만
```

---

### FromContext — 핵심 팩토리 메서드

`VisionContext.Data`(딕셔너리)에 쌓인 원시 결과를 타입화된 객체로 변환한다.

```
VisionContext.Data (Dictionary<string, object>)
 "VisionPro.Caliper.0"        → CogCaliperResults
 "VisionPro.Caliper.1"        → CogCaliperResults
 "VisionPro.Blob.0"           → CogBlobResults
 "VisionPro.CaliperDistance.0"→ CaliperDistanceResult
```

키를 **prefix 기반으로 스캔**하고 `OrderBy`로 정렬해서 순서를 보장한다.

#### Caliper 에지의 좌표 변환

```csharp
x = region.CenterX + Math.Cos(region.Rotation) * r.Position;
y = region.CenterY + Math.Sin(region.Rotation) * r.Position;
```

Caliper의 `Position`은 **스캔 방향의 1D 거리**다. Region의 중심점과 회전각을 이용해 이미지의 2D 좌표로 변환한다.

```
Region 중심 (CenterX, CenterY)
      ↓
      + Rotation 방향으로 Position만큼 이동
      ↓
이미지 좌표 (X, Y)
```

Region 정보가 없으면 `x = r.Position, y = 0`으로 폴백한다.

---

## CaliperEdge

Caliper 하나의 에지 검출 결과다. 두 가지 좌표를 모두 보존한다.

| 속성 | 의미 |
|---|---|
| `X`, `Y` | 이미지 2D 좌표 (변환 후) |
| `Position` | 스캔 축 1D 원시 위치 (변환 전) |
| `Score` | 에지 신뢰도 0~1 |
| `CaliperId` | 몇 번째 Caliper 스텝 (0-based) |
| `EdgeIndex` | 해당 스텝 내 몇 번째 에지 (0-based) |

`Position`을 별도로 보존하는 이유: 거리 계산(`CogCaliperDistanceStep`)이 2D 좌표가 아닌 1D Position을 사용하기 때문이다.

---

## BlobItem

```csharp
internal CogBlobResult RawResult { get; }
```

`Area`, `CenterX`, `CenterY`는 생성 시 값을 복사해두고,
`RawResult`는 `DisplayHelper`에서 외곽선 그래픽을 그릴 때 쓰기 위해 보관한다.
`internal`이므로 외부 앱에서는 접근할 수 없다.

---

## DistanceMeasurement

```csharp
public double Distance { get; }   // √((X2-X1)²+(Y2-Y1)²)
```

`CaliperDistanceResult`의 원시 값을 그대로 복사해서 보관한다.
`CogCaliperDistanceStep`이 이미 유클리드 거리를 계산해서 넘겨주므로 여기선 계산 없이 래핑만 한다.

---

## 전체 데이터 흐름 요약

```
파이프라인 실행
 │
 ├─ CogCaliperStep.Execute()      → context.Data["VisionPro.Caliper.0"]         = CogCaliperResults
 ├─ CogBlobStep.Execute()         → context.Data["VisionPro.Blob.0"]            = CogBlobResults
 └─ CogCaliperDistanceStep        → context.Data["VisionPro.CaliperDistance.0"] = CaliperDistanceResult
 │
 └─ VisionResult.FromContext(context, steps)
      │
      ├─ "VisionPro.Caliper.*"          → CaliperEdge 목록 (1D→2D 좌표 변환)
      ├─ "VisionPro.Blob.*"             → BlobItem 목록
      └─ "VisionPro.CaliperDistance.*"  → DistanceMeasurement 목록
      │
      └─ VisionResult (불변 객체) 반환
```
