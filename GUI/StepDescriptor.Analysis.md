# StepDescriptor.cs 분석

## 역할

스텝 유형에 대한 **메타데이터와 팩토리를 보유**하는 클래스다.
`PipelineController`와 `PipelineEditorForm`에서 팔레트 목록 구성에 사용한다.

```csharp
namespace Vision.UI

public class StepDescriptor
```

---

## 속성

| 속성 | 역할 |
|---|---|
| `DisplayName` | UI 목록에 표시할 이름 (예: "Caliper (에지 검출)") |
| `Category` | "Cognex" 또는 "OpenCV" |
| `TypeName` | `step.Name`과 동일 — XML 저장 키 (예: "VisionPro.Caliper") |
| `RequiredInputType` | 스텝의 요구 입력 타입 |
| `ProducedOutputType` | 스텝의 출력 타입 |

---

## 생성자 — 팩토리 호출 전략

```csharp
public StepDescriptor(string displayName, string category, Func<IVisionStep> factory)
{
    var temp = factory();                      // 임시 인스턴스 생성
    TypeName           = temp.Name;            // 이름 추출
    var typed          = temp as IImageTypedStep;
    RequiredInputType  = typed?.RequiredInputType  ?? ImageType.Any;
    ProducedOutputType = typed?.ProducedOutputType ?? ImageType.Any;
    (temp as IDisposable)?.Dispose();          // 즉시 해제
}
```

생성자에서 `factory()`를 **한 번만 호출**하여 TypeName과 이미지 타입 정보를 캐싱한다.
팔레트에 표시할 때마다 스텝 인스턴스를 만들지 않아도 된다.

실제 스텝 인스턴스는 `CreateStep()`을 호출할 때만 생성된다:
```csharp
public IVisionStep CreateStep() => _factory();
```

---

## ToString

```csharp
public override string ToString()
    => $"[{Category}] {DisplayName}  ({TypeLabel(RequiredInputType)}->{TypeLabel(ProducedOutputType)})";
```

예: `[Cognex] Caliper (에지 검출)  (Any->Any)`

`PipelineEditorForm`의 팔레트 ListBox에 이 형식으로 표시된다.

---

## PipelineManager와의 연결

`TypeName`이 XML의 `type` 속성과 일치하므로 로드 시 팩토리 조회에 사용된다:

```csharp
// PipelineController.Load()
var factories = new Dictionary<string, Func<IVisionStep>>();
foreach (var desc in _stepDescriptors)
    factories[desc.TypeName] = desc.CreateStep;
_manager.LoadAll(factories);
```
