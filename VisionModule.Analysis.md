# Vision Module 전체 클래스 분석

> 이미 별도 파일로 분석한 클래스는 해당 파일을 참조한다.
> - `ImageConverter` → `Converters/ImageConverter.Analysis.md`
> - `VisionResult` / `CaliperEdge` / `BlobItem` / `DistanceMeasurement` → `Results/VisionResult.Analysis.md`

---

## 전체 구조 개요

```
Vision Module (Vision.dll)
│
├── [Core Layer]        Core.cs
│     VisionContext, IVisionStep, IStepSerializable,
│     IRegionStep, IInspectionStep, IImageTypedStep, ImageType
│
├── [Pipeline Layer]    PipeLine.cs / PipelineConfig.cs
│     VisionPipeline, PipelineConfig
│
├── [Management Layer]  PipelineManager.cs / PipelineController.cs
│     PipelineManager, PipelineController
│
├── [Step Layer]        Steps/Cognex/ + Steps/OpenCV/
│     CogStepBase, CvStepBase (추상 기반)
│     CogCaliperStep, CogBlobStep, CogCaliperDistanceStep,
│     CogConvertGray, CogWeightedRGBStep, CvThresholdStep
│     CaliperDistanceResult (결과 데이터)
│
├── [Result Layer]      Results/
│     VisionResult, CaliperEdge, BlobItem, DistanceMeasurement
│
├── [Converter Layer]   Converters/
│     ImageConverter
│
└── [GUI Layer]         GUI/
      IStepParamPanel, StepDescriptor, DisplayHelper
      StepParamPanelFactory
      PipelineEditorForm
      Pipelines/ (각 스텝별 파라미터 패널 UserControl)
```

---

## Core Layer

### VisionContext

파이프라인 전체에서 **공유되는 컨테이너** 역할이다. 스텝 간 이미지와 결과를 주고받는 유일한 통로다.

```csharp
public class VisionContext : IDisposable
```

| 속성 | 역할 |
|---|---|
| `MatImage` | OpenCV Mat 이미지 |
| `CogImage` | VisionPro ICogImage 이미지 |
| `IsSuccess` | 파이프라인 성공 여부 (false면 중단) |
| `Errors` | 오류 메시지 누적 목록 |
| `Data` | 스텝 간 결과 딕셔너리 |

`MatImage`와 `CogImage` 중 하나만 설정해도 된다. 기반 클래스(`CogStepBase` / `CvStepBase`)가 실행 시 자동으로 변환한다.

`Data` 딕셔너리의 키 규칙:
```
"VisionPro.Caliper.0"        → CogCaliperResults
"VisionPro.Caliper.0.Region" → CogRectangleAffine
"VisionPro.Blob.0"           → CogBlobResults
"VisionPro.CaliperDistance.0"→ CaliperDistanceResult
```

---

### 인터페이스 계층

모든 인터페이스는 필요한 것만 골라 구현하는 **선택적 확장** 구조다.

```
IVisionStep (필수)
 ├── Name
 ├── ContinueOnFailure
 └── Execute(context)

IStepSerializable (선택 — XML 저장/로드)
 ├── SaveParams(XElement)
 └── LoadParams(XElement)

IRegionStep (선택 — ROI 보유)
 ├── Region
 └── RegionRequired

IInspectionStep (마커 인터페이스 — 검사 스텝 표시)

IImageTypedStep (선택 — 이미지 타입 선언)
 ├── RequiredInputType
 └── ProducedOutputType
```

`IInspectionStep`은 메서드가 없는 **마커 인터페이스**다. `PipelineEditorForm`이 단일 스텝 테스트 시 이 인터페이스 유무로 스텝 분류를 구분한다.

---

### ImageType 열거형

```csharp
public enum ImageType { Any, Grey, Color }
```

`PipelineEditorForm`이 스텝 순서 변경 시 인접 스텝 간 타입 불일치를 감지하는 데 사용한다.

호환 규칙:
- `Any` ← Any, Grey, Color (항상 허용)
- `Grey` ← Grey만
- `Color` ← Color만

---

## Pipeline Layer

### VisionPipeline

등록된 `IVisionStep`을 **순서대로 비동기 실행**하는 엔진이다.

```csharp
public class VisionPipeline : IDisposable
```

핵심 특징:
- 메서드 체이닝: `pipeline.AddStep(a).AddStep(b)`
- `Task.Run()`으로 각 스텝을 백그라운드 스레드에서 실행 → UI 블로킹 없음
- 스텝 예외는 `context.SetError()`로 변환하여 파이프라인 전체를 죽이지 않는다.
- `ContinueOnFailure = false`인 스텝 실패 → 즉시 중단
- `CancellationToken` 지원

실행 흐름:
```
for (각 스텝)
 ├─ CancellationToken 확인
 ├─ Task.Run(() => step.Execute(context))
 ├─ 예외 → context.SetError()
 └─ IsSuccess=false && ContinueOnFailure=false → break
```

`Dispose()`는 스텝 목록만 비운다. **스텝 인스턴스 자체는 해제하지 않는다** — 스텝은 `PipelineConfig`가 소유하기 때문이다.

---

### PipelineConfig

**하나의 파이프라인 설정**을 담는 단순 데이터 컨테이너다.

```csharp
public class PipelineConfig
{
    public string Name { get; set; }
    public List<IVisionStep> Steps { get; set; }
}
```

`VisionPipeline`과의 관계: `PipelineConfig.Steps`를 순서대로 `VisionPipeline.AddStep()`에 넘겨서 실행한다. `VisionPipeline`은 실행 엔진, `PipelineConfig`는 데이터다.

---

## Management Layer

### PipelineManager

여러 `PipelineConfig`를 관리하고 `pipelines.xml`로 저장/로드하는 관리자다.

```csharp
public class PipelineManager
```

주요 역할:
- `PipelineConfig` 목록 관리 (추가/삭제)
- 활성 인덱스 추적 (`ActiveIndex`) — 범위 벗어나지 않도록 자동 클램핑
- XML 저장/로드

**저장 (`SaveAll`)** 흐름:
```
root <Pipelines active="0">
 └─ <Pipeline name="기본">
     └─ <Steps>
         ├─ <Step type="VisionPro.Caliper">
         │   <Region> ... </Region>
         │   <RunParams> ... </RunParams>
         └─ <Step type="VisionPro.Blob">
             ...
```

**로드 (`LoadAll`)** 핵심: `stepFactories` 딕셔너리를 외부에서 주입받는다. Vision.dll이 GUI 어셈블리에 의존하지 않도록 의존성을 역전한 것이다.
```csharp
public void LoadAll(IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
```

알 수 없는 `type`의 스텝은 조용히 건너뛴다 — 플러그인 제거나 버전 불일치 시 크래시를 방지한다.

---

### PipelineController

외부 앱이 사용하는 **단일 진입점(Facade)** 클래스다. Vision Module의 모든 기능을 여기 하나로 통합한다.

```csharp
public class PipelineController
```

제공 기능:

| 메서드 | 역할 |
|---|---|
| `ShowEditor()` | 파이프라인 편집 다이얼로그 표시 |
| `RunAsync(image)` | 활성 파이프라인 실행 → `VisionResult` 반환 |
| `DrawResults(display, result)` | 결과 그래픽 표시 |
| `GetParamPanel(step)` | 스텝 파라미터 편집 UserControl 반환 |
| `ApplyParamPanel(step, panel)` | 패널 값 → 스텝 반영 (저장 없음) |
| `ApplyAndSave(step, panel)` | 반영 + 파일 저장 |
| `Save()` / `Load()` | 전체 파이프라인 저장/로드 |
| `AddPipeline()` / `RemovePipeline()` | 파이프라인 CRUD |
| `DuplicateActivePipeline()` | XML 직렬화 경유 깊은 복사 |
| `RegisterStep()` | 커스텀 스텝 팔레트 등록 |

`DuplicateActivePipeline()`의 깊은 복사 방법이 특이하다:
```csharp
// XML을 중간 매개체로 사용하여 파라미터를 완전히 복사
srcSerial.SaveParams(el);
dstSerial.LoadParams(el);
```

`EnsureActivePipeline()`: `ShowEditor()` 호출 전 파이프라인이 없으면 "기본 파이프라인"을 자동 생성한다.

---

## Step Layer — 추상 기반 클래스

### CogStepBase (Cognex 스텝 기반)

```csharp
public abstract class CogStepBase : IVisionStep, IImageTypedStep
```

핵심 역할: **MatImage → CogImage 자동 변환**

```
Execute(context)
 ├─ CogImage == null && MatImage != null
 │   → ToCogImage8Grey(MatImage)
 │   → MatImage.Dispose()
 └─ ExecuteCore(context)   ← 하위 클래스 구현
```

기본값:
- `ContinueOnFailure = false`
- `RequiredInputType = Any`
- `ProducedOutputType = Any`

---

### CvStepBase (OpenCV 스텝 기반)

```csharp
public abstract class CvStepBase : IVisionStep, IImageTypedStep
```

핵심 역할: **CogImage → MatImage 자동 변환** (CogStepBase와 대칭)

```
Execute(context)
 ├─ MatImage == null && CogImage != null
 │   → ToMat(CogImage)
 │   → CogImage.Dispose()
 └─ ExecuteCore(context)   ← 하위 클래스 구현
```

기본값:
- `ProducedOutputType = Grey` (OpenCV 스텝은 기본적으로 그레이 출력)

---

### CogStepBase vs CvStepBase 비교

| | CogStepBase | CvStepBase |
|---|---|---|
| 자동 변환 방향 | Mat → Cog | Cog → Mat |
| 기본 출력 타입 | Any | Grey |
| 내부 툴 | CognexTool 직접 사용 | OpenCV Cv2.* 사용 |

---

## Step Layer — 구현 클래스

### CogCaliperStep

에지(밝기 경계)를 검출하는 스텝이다.

- 구현 인터페이스: `CogStepBase, IStepSerializable, IRegionStep, IInspectionStep`
- 결과 키: `"VisionPro.Caliper.{N}"` (N 자동 증가)
- Region도 함께 저장: `"VisionPro.Caliper.{N}.Region"` — `CogCaliperDistanceStep`이 좌표 변환에 사용

XML 직렬화:
- `Region`: CenterX/Y, SideXLength/Y, Rotation, Skew
- `RunParams`: ContrastThreshold, EdgeMode, Edge0Polarity, FilterHalfSizeInPixels, MaxResults

`InvariantCulture`로 실수를 직렬화한다 — 로케일(소수점 기호 차이)에 무관하게 파일 이식 가능.

---

### CogBlobStep

이진화 후 연결된 픽셀 영역(Blob)을 검출하는 스텝이다.

- 구현 인터페이스: `CogStepBase, IStepSerializable, IRegionStep, IInspectionStep`
- 결과 키: `"VisionPro.Blob.{N}"`
- 요구 입력: `ImageType.Grey` (그레이스케일만)

세그멘테이션 모드:

| 모드 | 설명 |
|---|---|
| HardFixedThreshold | 단일 고정 임계값 |
| SoftFixedThreshold | 상/하한 임계값 범위 |
| HardRelativeThreshold | 이미지 평균 기준 상대값 |
| SoftRelativeThreshold | 이미지 평균 기준 상/하한 |

---

### CogCaliperDistanceStep

파이프라인 내 두 Caliper 결과 사이의 **유클리드 거리**를 계산하는 스텝이다.

- `CogStepBase`를 상속하지 않는다 — 이미지를 처리하지 않고 `context.Data`만 읽기 때문
- 직접 `IVisionStep`을 구현한다.
- 결과 키: `"VisionPro.CaliperDistance.{N}"`

동작 흐름:
```
context.Data["VisionPro.Caliper.{CaliperId_A}"] → Position_A
context.Data["VisionPro.Caliper.{CaliperId_A}.Region"] → Region_A
Position_A + Region_A → (X1, Y1)  ← 1D→2D 좌표 변환

context.Data["VisionPro.Caliper.{CaliperId_B}"] → Position_B
Position_B + Region_B → (X2, Y2)

√((X2-X1)²+(Y2-Y1)²) → CaliperDistanceResult
```

---

### CaliperDistanceResult

`CogCaliperDistanceStep`의 결과 데이터 클래스다.

`Distance` 속성이 `get`에서 직접 계산한다:
```csharp
public double Distance =>
    Math.Sqrt((X2-X1)*(X2-X1) + (Y2-Y1)*(Y2-Y1));
```
저장된 값이 아닌 파생 계산값이다.

---

### CogConvertGray

컬러 이미지(`CogImage24PlanarColor`)에서 단일 채널을 추출하여 그레이스케일로 변환한다.

- `GetPlane(index)` 사용: 0=Red, 1=Green, 2=Blue
- 입력: `ImageType.Color`, 출력: `ImageType.Grey`
- 기본 채널: Green

활용: 조명 조건에 맞는 채널 선택 (적색 조명 → Red 채널이 대비 높음)

---

### CogWeightedRGBStep

R/G/B 채널에 각각 가중치를 곱해 합산하는 고급 컬러→그레이 변환 스텝이다.

처리 순서:
```
R 채널 → ApplyWeight(planeR, RedWeight)   → weightedR
G 채널 → ApplyWeight(planeG, GreenWeight) → weightedG
B 채널 → ApplyWeight(planeB, BlueWeight)  → weightedB

_addRG:    weightedR + weightedG → 중간 합
_addFinal: 중간 합 + weightedB  → 최종 그레이
```

`ApplyWeight()`: `Get8GreyPixelMemory`로 직접 픽셀 접근 + `Marshal.Copy`로 가중치 적용. `ImageConverter`와 동일한 직접 메모리 접근 패턴이다.

기본값: R=G=B=1/3 (균등 합산). BT.601 표준 휘도: R=0.299, G=0.587, B=0.114

---

### CvThresholdStep

OpenCV `Cv2.Threshold()`를 사용한 이진화 스텝이다.

- `CvStepBase` 상속 → CogImage가 있으면 자동으로 Mat으로 변환 후 실행
- 결과 Mat으로 `context.MatImage`를 교체, `context.CogImage`를 null로 설정

주요 타입:

| ThresholdTypes | 설명 |
|---|---|
| Binary | pixel > thresh → MaxValue |
| BinaryInv | pixel > thresh → 0 |
| Otsu | 자동 최적 임계값 (ThresholdValue 무시) |
| Triangle | 삼각형 알고리즘 자동 임계값 |

---

## GUI Layer

### IStepParamPanel

파라미터 편집 UserControl이 구현하는 인터페이스다.

```csharp
public interface IStepParamPanel
{
    void BindStep(IVisionStep step);   // 스텝 값 → UI 반영
    void FlushStep(IVisionStep step);  // UI 값 → 스텝 반영
}
```

`PipelineEditorForm` 내부와 외부 앱에서 **동일하게** 사용된다.

---

### StepDescriptor

스텝 유형에 대한 메타데이터를 보유하는 클래스다.

```csharp
public class StepDescriptor
{
    string DisplayName        // UI 표시 이름
    string Category           // "Cognex" / "OpenCV"
    string TypeName           // step.Name과 동일 (XML 저장 키)
    ImageType RequiredInputType
    ImageType ProducedOutputType
    IVisionStep CreateStep()  // 팩토리 호출
}
```

생성자에서 `factory()`를 **한 번 호출**하여 `TypeName`과 이미지 타입 정보를 추출한 뒤 Dispose한다. 매번 실제 스텝을 만들지 않고 메타데이터를 캐싱하는 구조다.

`ToString()`: `[Cognex] Caliper (Any->Any)` 형식으로 팔레트에 표시된다.

---

### StepParamPanelFactory

스텝 이름으로 파라미터 패널을 생성하는 정적 팩토리다.

```csharp
public static Control Create(IVisionStep step)
{
    switch (step?.Name)
    {
        case "VisionPro.Caliper":         return new CogCaliperParamPanel();
        case "VisionPro.Blob":            return new CogBlobParamPanel();
        case "VisionPro.ConvertGray":     return new CogConvertGrayParamPanel();
        case "VisionPro.CaliperDistance": return new CogCaliperDistanceParamPanel();
        default: return null;
    }
}
```

새 스텝 추가 시 여기에 case 하나만 추가하면 된다.

---

### DisplayHelper

CogDisplay에 검사 결과 그래픽을 그리는 **정적 유틸** 클래스다.

| 메서드 | 그래픽 | 색상 |
|---|---|---|
| `DrawEdge()` | 십자 마커 (±10px) | 초록 |
| `DrawBlob()` | 외곽선 폴리곤 | 노란 |
| `DrawDistance()` | 직선 + 양 끝 십자 마커 | 마젠타 |
| `DrawAllResults()` | 위 세 개를 한 번에 | — |

`StaticGraphics.Add(graphic, key)` — key는 `"edge_0_0_h"`, `"blob_0_1_boundary"` 형식이다. 같은 key로 Add하면 덮어쓰인다.

내부 `internal` 메서드들(`DrawEdgeMarkerOnDisplay` 등)은 `PipelineEditorForm`이 원시 타입(`CogCaliperResult`)으로 직접 그릴 때 사용한다.

---

## 전체 데이터 흐름

```
외부 앱
 │
 │ controller.RunAsync(image)
 ▼
PipelineController
 │ VisionPipeline.RunAsync(context)
 ▼
VisionContext { CogImage = image }
 │
 ├─ CogConvertGray.Execute()   → context.CogImage = 그레이
 ├─ CogCaliperStep.Execute()   → context.Data["VisionPro.Caliper.0"]
 ├─ CogBlobStep.Execute()      → context.Data["VisionPro.Blob.0"]
 └─ CogCaliperDistanceStep     → context.Data["VisionPro.CaliperDistance.0"]
 │
 │ VisionResult.FromContext(context, steps)
 ▼
VisionResult { CaliperEdges, Blobs, Distances }
 │
 │ controller.DrawResults(display, result)
 ▼
DisplayHelper → CogDisplay.StaticGraphics
```
