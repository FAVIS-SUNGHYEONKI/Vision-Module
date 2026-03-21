# DisplayHelper.cs 분석

## 역할

`CogDisplay`에 검사 결과 그래픽을 그리는 **정적 유틸 클래스**다.

```csharp
namespace Vision.UI

public static class DisplayHelper
```

`PipelineController.DrawResults()`와 `PipelineEditorForm` 내부에서 공통으로 사용한다.

---

## 공개 메서드

### DrawAllResults

```csharp
public static void DrawAllResults(CogDisplay display, VisionResult result)
```

`VisionResult`의 모든 결과를 한 번에 그린다.
`StaticGraphics.Clear()`는 **호출자가 책임**진다 (`PipelineController.DrawResults()`에서 처리).

---

### DrawEdge — 에지 마커

```csharp
public static void DrawEdge(CogDisplay display, CaliperEdge edge)
```

에지 위치에 **초록 십자 마커(±10px)**를 그린다.

```
가로선: (X-10, Y) ~ (X+10, Y)
세로선: (X, Y-10) ~ (X, Y+10)

그래픽 키: "edge_{CaliperId}_{EdgeIndex}_h"
           "edge_{CaliperId}_{EdgeIndex}_v"
```

---

### DrawBlob — Blob 외곽선

```csharp
public static void DrawBlob(CogDisplay display, BlobItem blob)
```

`blob.RawResult.GetBoundary()`로 **노란 외곽선 폴리곤**을 그린다.

```
그래픽 키: "blob_{BlobStepId}_{BlobIndex}_boundary"
```

`RawResult`가 null이거나 `GetBoundary()`가 null을 반환하면 조용히 건너뛴다.

---

### DrawDistance — 거리 측정선

```csharp
public static void DrawDistance(CogDisplay display, DistanceMeasurement dist, int index)
```

두 점을 잇는 **마젠타 직선** + 양 끝에 **마젠타 십자 마커(±6px)**를 그린다.

```
그래픽 키: "dist_{index}_line"
           "dist_{index}_a_h", "dist_{index}_a_v"
           "dist_{index}_b_h", "dist_{index}_b_v"
```

---

## 그래픽 키 역할

`StaticGraphics.Add(graphic, key)`에서 key는 **덮어쓰기 식별자**다.
같은 key로 Add하면 이전 그래픽이 교체된다.
검사를 반복 실행할 때 이전 결과 그래픽이 자동으로 갱신된다.

---

## internal 메서드 — PipelineEditorForm 전용

| 메서드 | 용도 |
|---|---|
| `CaliperResultToXY(result, region)` | 에지 위치를 문자열로 변환 |
| `DrawEdgeMarkerOnDisplay(...)` | 원시 `CogCaliperResult`로 마커 그리기 |
| `DrawBlobMarkerOnDisplay(...)` | 원시 `CogBlobResult`로 외곽선 그리기 |
| `DrawDistanceLineOnDisplay(...)` | 원시 `CaliperDistanceResult`로 거리선 그리기 |

`PipelineEditorForm`이 단일 스텝 테스트 시 `VisionResult`를 거치지 않고 직접 원시 결과 타입으로 그릴 때 사용한다.

---

## DrawCross — 내부 유틸

```csharp
private static void DrawCross(CogDisplay display, double x, double y,
    int size, CogColorConstants color, string keyPrefix)
```

십자 마커를 그리는 공통 로직. `DrawEdge`와 `DrawDistance` 양쪽에서 재사용된다.
