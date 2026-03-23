# Core.cs 분석

파일 하나에 **8개의 타입**이 정의되어 있다.

---

## VisionContext

파이프라인 실행 중 스텝 간 이미지와 결과를 전달하는 공유 컨테이너. `IDisposable` 구현.

| 프로퍼티 | 타입 | 설명 |
|----------|------|------|
| `MatImage` | `Mat` | OpenCV Mat 이미지. CvStepBase 계열 사용 |
| `CogImage` | `ICogImage` | VisionPro 이미지. CogStepBase 계열 사용 |
| `IsSuccess` | `bool` | 파이프라인 전체 성공 여부 (기본 true) |
| `Errors` | `List<string>` | 스텝별 오류 메시지 누적 목록 |
| `Data` | `Dictionary<string, object>` | 스텝 결과 저장소 |

**Data 키 규칙:**
```
"VisionPro.Caliper.0"            → CogCaliperResultCollection
"VisionPro.Caliper.0.Region"     → CogRectangleAffine
"VisionPro.Blob.0"               → CogBlobResults
"VisionPro.CaliperDistance.0"    → CaliperDistanceResult
```
동일 타입 스텝이 여러 개이면 숫자가 자동 증가한다.

---

## IVisionStep

모든 스텝의 핵심 계약 인터페이스.

```csharp
public interface IVisionStep
{
    string Name              { get; }       // 스텝 식별자 (XML type 속성 키)
    string DisplayName       { get; set; }  // 사용자 지정 표시 이름 (미설정 시 Name 반환)
    bool   ContinueOnFailure { get; }       // 실패 후 파이프라인 계속 여부
    void   Execute(VisionContext context);
}
```

> `DisplayName`은 초기 구조 대비 **추가된 멤버**. 스텝마다 사용자 정의 이름을 부여하고
> GUI 목록/결과 탭에 표시하기 위해 도입됨. `PipelineManager`는 `Name`과 다를 때만 `label` 속성으로 XML에 저장한다.

---

## 선택적 확장 인터페이스

| 인터페이스 | 핵심 멤버 | 용도 | 주요 구현체 |
|-----------|-----------|------|------------|
| `IStepSerializable` | `SaveParams(XElement)`, `LoadParams(XElement)` | XML 파라미터 저장/복원 | CogCaliperStep, CogBlobStep, CvThresholdStep 등 |
| `IRegionStep` | `Region`, `RegionRequired` | 검사 영역(ROI) 보유 | CogCaliperStep, CogBlobStep |
| `IInspectionStep` | _(마커)_ | 검사 결과를 Data에 기록하는 스텝 표시. 단일 스텝 테스트 시 이전 IInspectionStep 스킵 판단 기준 | CogCaliperStep, CogBlobStep |
| `IImageTypedStep` | `RequiredInputType`, `ProducedOutputType` | 스텝 간 이미지 타입 호환 경고 | CogStepBase·CvStepBase 파생 클래스 |

---

## ImageType 열거형

```csharp
public enum ImageType { Any, Grey, Color }
```

호환 규칙: `Any`는 모두 허용, `Grey`↔`Grey`, `Color`↔`Color`만 호환.
`PipelineEditorForm`이 스텝 추가/순서 변경 시 불일치 경고에 사용한다.
