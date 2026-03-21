# PipelineConfig.cs 분석

## 역할

**하나의 파이프라인 설정**을 담는 단순 데이터 컨테이너다.

```csharp
public class PipelineConfig
{
    public string Name { get; set; } = "Pipeline";
    public List<IVisionStep> Steps { get; set; } = new List<IVisionStep>();
}
```

---

## 설계 의도

로직이 전혀 없는 순수 데이터 객체(POCO)다.
이름과 스텝 목록만 보유하며, 실행은 `VisionPipeline`이, 관리는 `PipelineManager`가 담당한다.

| 역할 | 담당 클래스 |
|---|---|
| 데이터 보관 | `PipelineConfig` |
| 실행 | `VisionPipeline` |
| 저장/로드/관리 | `PipelineManager` |

---

## 관계

```
PipelineManager
 └─ List<PipelineConfig>
      └─ PipelineConfig
          ├─ Name       → ComboBox 표시, XML name 속성
          └─ Steps      → VisionPipeline.AddStep()에 순서대로 전달
```

---

## XML 저장 형식

`PipelineManager.SaveAll()`이 이 객체를 아래 XML로 변환한다.

```xml
<Pipeline name="기본 파이프라인">
  <Steps>
    <Step type="VisionPro.Caliper">
      <Region>...</Region>
      <RunParams>...</RunParams>
    </Step>
    <Step type="VisionPro.Blob">
      ...
    </Step>
  </Steps>
</Pipeline>
```
