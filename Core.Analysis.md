# Core.cs 분석

파일 하나에 **7개의 타입**이 정의되어 있다.

```
VisionContext        — 파이프라인 공유 컨테이너
IVisionStep          — 모든 스텝의 필수 계약
IStepSerializable    — XML 저장/로드 (선택)
IRegionStep          — ROI 보유 스텝 (선택)
IInspectionStep      — 검사 스텝 마커 (선택)
IImageTypedStep      — 이미지 타입 선언 (선택)
ImageType            — Grey / Color / Any 열거형
```

---

## VisionContext

파이프라인 전체에서 **공유되는 컨테이너**다. 스텝 간 이미지와 결과를 주고받는 유일한 통로다.

```csharp
public class VisionContext : IDisposable
```

### 속성

| 속성 | 타입 | 역할 |
|---|---|---|
| `MatImage` | `Mat` | OpenCV 이미지 |
| `CogImage` | `ICogImage` | VisionPro 이미지 |
| `IsSuccess` | `bool` | 파이프라인 성공 여부 (기본 true) |
| `Errors` | `List<string>` | 오류 메시지 누적 목록 |
| `Data` | `Dictionary<string, object>` | 스텝 간 결과 전달 딕셔너리 |

`MatImage`와 `CogImage` 중 하나만 설정해도 된다.
기반 클래스(`CogStepBase` / `CvStepBase`)가 실행 시 자동으로 변환한다.

### Data 딕셔너리 키 규칙

```
"VisionPro.Caliper.0"         → CogCaliperResults
"VisionPro.Caliper.0.Region"  → CogRectangleAffine
"VisionPro.Blob.0"            → CogBlobResults
"VisionPro.CaliperDistance.0" → CaliperDistanceResult
```

동일 타입 스텝이 여러 개면 인덱스가 자동 증가한다.

### SetError

```csharp
public void SetError(string message)
{
    IsSuccess = false;
    Errors.Add(message);
}
```

스텝이 실패를 기록할 때 사용한다. 예외를 throw하는 대신 이 메서드를 호출해야 한다.

### Dispose

```csharp
public void Dispose()
{
    MatImage?.Dispose();
    (CogImage as IDisposable)?.Dispose();
}
```

`ICogImage`는 `IDisposable`을 직접 구현하지 않으므로 캐스팅 후 해제한다.

---

## IVisionStep

모든 스텝이 **반드시** 구현해야 하는 핵심 계약이다.

```csharp
public interface IVisionStep
{
    string Name { get; }
    bool ContinueOnFailure { get; }
    void Execute(VisionContext context);
}
```

| 멤버 | 역할 |
|---|---|
| `Name` | 스텝 식별자. XML 저장 시 `type` 속성으로 사용 |
| `ContinueOnFailure` | false = 실패 시 파이프라인 중단 |
| `Execute()` | 실제 처리 로직. 오류는 `context.SetError()` 호출 |

---

## 선택적 확장 인터페이스

### IStepSerializable

XML 저장/로드가 필요한 스텝이 구현한다.

```csharp
public interface IStepSerializable
{
    void SaveParams(XElement el);   // 파라미터 → XML
    void LoadParams(XElement el);   // XML → 파라미터
}
```

`PipelineManager.SaveAll()` / `LoadAll()`이 이 인터페이스 유무를 확인하고 호출한다.

---

### IRegionStep

검사 영역(ROI)을 보유하는 스텝이 구현한다.

```csharp
public interface IRegionStep
{
    CogRectangleAffine Region { get; set; }
    bool RegionRequired { get; }
}
```

- `Region = null` + `RegionRequired = false` → 전체 이미지 대상으로 실행
- `PipelineEditorForm`이 이 인터페이스로 Region 설정 UI를 연결한다.

---

### IInspectionStep

메서드가 없는 **마커 인터페이스**다.

```csharp
public interface IInspectionStep { }
```

`PipelineEditorForm`의 단일 스텝 테스트 시 이 인터페이스를 구현한 스텝 이전에는
이미지 변환 스텝만 선행 실행된다. 구현체: `CogCaliperStep`, `CogBlobStep`

---

### IImageTypedStep

스텝의 입출력 이미지 타입을 선언한다.

```csharp
public interface IImageTypedStep
{
    ImageType RequiredInputType  { get; }
    ImageType ProducedOutputType { get; }
}
```

`PipelineEditorForm`이 스텝 순서 변경 시 인접 스텝 간 타입 불일치를 감지하여 경고를 표시한다.
이 인터페이스를 구현하지 않으면 `Any → Any`로 취급한다.

---

## ImageType

```csharp
public enum ImageType { Any, Grey, Color }
```

호환 규칙:
- `Any` ← Any, Grey, Color (항상 허용)
- `Grey` ← Grey만
- `Color` ← Color만

---

## 인터페이스 조합 패턴

각 스텝이 필요한 인터페이스만 골라 구현한다.

| 스텝 | 구현 인터페이스 |
|---|---|
| `CogCaliperStep` | `IVisionStep` + `IStepSerializable` + `IRegionStep` + `IInspectionStep` + `IImageTypedStep` |
| `CogCaliperDistanceStep` | `IVisionStep` + `IStepSerializable` + `IInspectionStep` + `IImageTypedStep` |
| `CogConvertGray` | `IVisionStep` + `IStepSerializable` + `IImageTypedStep` |
