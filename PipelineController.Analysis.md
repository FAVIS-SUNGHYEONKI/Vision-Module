# PipelineController.cs 분석

## 역할

외부 앱(Vision-GUI)이 사용하는 **단일 진입점(Facade)** 클래스다.
파이프라인 편집 UI 표시, 파이프라인 실행, 결과 렌더링, 저장/로드를 하나의 API로 제공한다.

---

## 핵심 필드

| 필드 | 설명 |
|------|------|
| `_manager` | `PipelineManager` — 파이프라인 설정 저장/로드 관리 |
| `_stepDescriptors` | 등록된 스텝 팔레트 목록 |
| `_cachedPipeline` | `VisionPipeline` 캐시 — 스텝 구성이 변경될 때만 재생성 |

---

## VisionPipeline 캐싱

초기 구조에서는 `RunAsync` 호출마다 `new VisionPipeline()`을 생성했다. 현재는 캐싱 구조로 변경됨.

```csharp
private VisionPipeline _cachedPipeline;

private void RebuildPipeline()
{
    _cachedPipeline?.Dispose();
    _cachedPipeline = new VisionPipeline();
    foreach (var step in _manager.ActivePipeline?.Steps ?? ...)
        _cachedPipeline.AddStep(step);
}
```

**`RebuildPipeline()`이 호출되는 시점:**
1. `ActivePipelineIndex` setter — 파이프라인 전환 시
2. `Load()` — XML에서 설정 복원 후
3. `ShowEditor()` OK 반환 후 — staged → real 반영 후

**RunAsync 에서:**
```csharp
if (_cachedPipeline == null) RebuildPipeline();
await _cachedPipeline.RunAsync(ctx);
```

---

## ShowEditor

```csharp
public DialogResult ShowEditor(IWin32Window owner, ICogImage inputImage)
```

- `PipelineEditorForm`을 다이얼로그로 열어 스텝 구성 편집
- OK 반환 시: `_manager.ActiveIndex = form.SelectedPipelineIndex` + `RebuildPipeline()`
- 편집 내용은 폼 안에서 `_stagedByPipeline`에 보관되다가 OK 시 실제 `_config.Steps`에 반영되고 `pipelines.xml`로 저장됨

---

## RunAsync

```csharp
public async Task<VisionResult> RunAsync(ICogImage image)
```

- 파이프라인이 없거나 비어 있으면 `VisionResult.Empty` 반환
- `_cachedPipeline`을 재사용 (매번 생성 안 함)
- `VisionContext`는 매 실행마다 새로 생성하고 `using`으로 자동 해제

---

## 파이프라인 CRUD

| 메서드 | 설명 |
|--------|------|
| `AddPipeline(name)` | 새 파이프라인 추가 |
| `DuplicateActivePipeline(newName)` | 활성 파이프라인 deep copy (IStepSerializable 경유) |
| `RemovePipeline(index)` | 삭제. 마지막 하나이면 `false` 반환 |
| `RenamePipeline(index, newName)` | 이름 변경 |

---

## 파라미터 패널 API

| 메서드 | 설명 |
|--------|------|
| `GetParamPanel(step)` | `StepParamPanelFactory`로 패널 생성 + `BindStep` 호출 |
| `ApplyParamPanel(step, panel)` | 패널 값을 스텝에 반영 (디스크 저장 안 함) |
| `ApplyAndSave(step, panel)` | 반영 + `SaveAll()` |

---

## 기본 스텝 등록 (`RegisterBuiltinSteps`)

| 표시 이름 | 카테고리 | 스텝 클래스 |
|-----------|----------|------------|
| ConvertGray (컬러→회색) | Cognex | `CogConvertGray` |
| Caliper (에지 검출) | Cognex | `CogCaliperStep` |
| Blob (영역 검출) | Cognex | `CogBlobStep` |
| CaliperDistance (거리 측정) | Cognex | `CogCaliperDistanceStep` |
| Threshold (이진화) | OpenCV | `CvThresholdStep` |
