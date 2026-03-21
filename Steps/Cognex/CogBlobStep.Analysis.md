# CogBlobStep.cs 분석

## 역할

VisionPro `CogBlobTool`을 래핑하여 **연결된 픽셀 영역(Blob)을 검출**하는 스텝이다.

```csharp
public class CogBlobStep : CogStepBase, IStepSerializable, IRegionStep, IInspectionStep
```

---

## CogCaliperStep과의 차이

| | CogCaliperStep | CogBlobStep |
|---|---|---|
| 검출 대상 | 밝기 경계(에지) | 연결된 픽셀 영역 |
| 필요 입력 | Any | **Grey** (그레이스케일만) |
| Region 저장 여부 | 저장 (거리 계산용) | 저장 안 함 |
| 결과 타입 | `CogCaliperResults` | `CogBlobResults` |

---

## 주요 속성

| 속성 | 역할 |
|---|---|
| `Name` | `"VisionPro.Blob"` |
| `RequiredInputType` | `ImageType.Grey` |
| `RunParams` | `CogBlob` — 세그멘테이션 파라미터 |
| `Region` | 검색 영역 (null이면 전체 이미지) |

---

## ExecuteCore — 실행

```csharp
protected override void ExecuteCore(VisionContext context)
{
    _tool.InputImage = context.CogImage;
    _tool.Run();

    var results = _tool.Results;
    if (results != null && results.GetBlobs().Count > 0)
    {
        int idx = 0;
        while (context.Data.ContainsKey(Name + "." + idx)) idx++;
        context.Data[Name + "." + idx] = results;
    }
    else
        context.SetError($"{Name}: Blob 결과 없음.");
}
```

`CogCaliperStep`과 동일한 **키 자동 증가 패턴**이다. Region은 별도 저장하지 않는다.

---

## 세그멘테이션 모드

`RunParams.SegmentationParams.Mode`로 제어한다:

| 모드 | 설명 |
|---|---|
| `HardFixedThreshold` | 단일 고정 임계값 |
| `SoftFixedThreshold` | 상/하한 임계값 범위 (Low ~ High) |
| `HardRelativeThreshold` | 이미지 평균 기준 상대 임계값 |
| `SoftRelativeThreshold` | 이미지 평균 기준 상/하한 |

---

## XML 직렬화 필드

**Region:** `CenterX`, `CenterY`, `SideXLength`, `SideYLength`, `Rotation`, `Skew`

**SegmentationParams:**
- `Mode`, `Polarity`
- `HardFixedThreshold`
- `SoftFixedThresholdLow`, `SoftFixedThresholdHigh`

**기타:** `ConnectivityMinPixels` — 최소 픽셀 수 필터

---

## 결과 활용

```csharp
// DisplayHelper에서 외곽선 그리기
var boundary = blob.RawResult?.GetBoundary();   // CogPolygon
display.StaticGraphics.Add(boundary, "blob_0_0_boundary");

// VisionResult에서 접근
foreach (var blob in result.Blobs)
    Console.WriteLine($"Area={blob.Area}, Center=({blob.CenterX},{blob.CenterY})");

// 특정 스텝 결과만 필터
var step0Blobs = result.GetBlobs(0);
```
