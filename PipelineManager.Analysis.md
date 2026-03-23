# PipelineManager.cs 분석

## 역할

여러 `PipelineConfig`를 관리하고 `pipelines.xml`로 저장/로드하는 관리자다.
GUI(PipelineEditorForm)와 Core(PipelineController) 사이의 중간 계층.

---

## 주요 구조

```
PipelineManager
  ├── _configs : List<PipelineConfig>
  ├── _activeIndex : int
  └── _folder : string  →  {folder}/pipelines.xml
```

| 프로퍼티 | 설명 |
|----------|------|
| `Configs` | 관리 중인 파이프라인 목록 (읽기 전용) |
| `ActivePipeline` | 현재 활성 파이프라인 (`null` 안전) |
| `ActiveIndex` | 활성 인덱스. 0 이상 Count-1 이하로 자동 클램핑 |
| `FilePath` | `{_folder}/pipelines.xml` |

---

## SaveAll

```csharp
public void SaveAll()
```

- `Directory.CreateDirectory(_folder)` — 폴더 자동 생성
- 루트 요소: `<Pipelines active="{activeIndex}">`
- 각 파이프라인: `<Pipeline name="{cfg.Name}">`
- 각 스텝: `<Step type="{step.Name}">`
  - `step.DisplayName != step.Name`일 때만 `label` 속성 추가 → 사용자 지정 이름 저장
  - `IStepSerializable`이면 `SaveParams(stepEl)` 호출

**XML 예시:**
```xml
<Pipelines active="0">
  <Pipeline name="검사 라인 A">
    <Steps>
      <Step type="VisionPro.Caliper" label="좌측 에지">
        <Region>...</Region>
        <RunParams>...</RunParams>
      </Step>
    </Steps>
  </Pipeline>
</Pipelines>
```

---

## LoadAll

```csharp
public void LoadAll(IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
```

- XML 파일 없으면 아무 작업도 안 함
- `type` 속성으로 팩토리 조회, 없으면 해당 스텝 건너뜀
- `label` 속성 있으면 `step.DisplayName` 복원
- `IStepSerializable`이면 `LoadParams(stepEl)` 호출
- 마지막으로 `_activeIndex` 클램핑

**팩토리 독립성:** Vision.dll은 GUI 어셈블리에 의존하지 않는다.
팩토리 딕셔너리는 `PipelineController.Load()`에서 `_stepDescriptors` 기반으로 빌드하여 주입한다.
