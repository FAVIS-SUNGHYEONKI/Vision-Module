# PipelineEditorForm.cs 분석

## 역할

파이프라인 목록 관리 + 스텝 구성 + 파라미터 편집 + 단일/전체 테스트를 하나의 다이얼로그에서 처리하는 폼.
`PipelineController.ShowEditor()`로만 호출되며, `DialogResult.OK` 반환 시 변경 사항이 PipelineManager에 반영된다.

```csharp
namespace Vision.UI

internal PipelineEditorForm(
    List<StepDescriptor> availableSteps,
    PipelineManager      pipelineManager,
    ICogImage            inputImage = null)
```

---

## 주요 필드

| 필드 | 타입 | 역할 |
|---|---|---|
| `_available` | `List<StepDescriptor>` | 팔레트(Cognex/OpenCV ListBox)에 표시할 스텝 목록 |
| `_inputImage` | `ICogImage` | 테스트용 입력 이미지 (null 가능) |
| `_pipelineManager` | `PipelineManager` | 파이프라인 목록 저장소 |
| `_config` | `PipelineConfig` | 현재 편집 중인 파이프라인 |
| `_currentPipelineIdx` | `int` | 현재 선택된 파이프라인 인덱스 |
| `PipelineSteps` | `List<IVisionStep>` | 현재 편집 중인 스텝 목록 (메모리 편집용 복사본) |
| `_currentPanel` | `IStepParamPanel` | 현재 표시 중인 파라미터 패널 |
| `_currentPanelStepIdx` | `int` | 패널이 바인딩된 스텝 인덱스 |
| `_testRegion` | `CogRectangleAffine` | 테스트 디스플레이에 표시 중인 Region |

---

## 초기화 흐름

```
InitializeComponent()
InitTestDisplay()          → cogTestDisplay를 코드로 동적 생성 (Location: 12, 534 / Size: 800x340)
팔레트 ListBox 채우기      → _available를 Category("Cognex"/"OpenCV")로 분류
RefreshPipelineComboInEditor()
SwitchToPipeline(initIdx)  → pipelineManager.ActiveIndex로 초기 파이프라인 선택
```

`PipelineEditorForm_Load`에서 `_inputImage` 유무에 따라 버튼 활성화 결정.

---

## 파이프라인 CRUD

### SwitchToPipeline(idx)

```csharp
_config       = _pipelineManager.Configs[idx];
PipelineSteps = new List<IVisionStep>(_config.Steps);  // 얕은 복사 (스텝 인스턴스는 공유)
ClearParamPanel();
RefreshPipelineList();
```

`_config.Steps`의 **얕은 복사**를 유지 — 스텝 인스턴스는 원본과 동일 객체를 참조하므로 즉시 편집이 반영됨.
디스크에 쓰기 전에 `CommitCurrentSteps()`로 `_config.Steps`에 재반영해야 함.

### CommitCurrentSteps()

```csharp
FlushCurrentPanel();                               // 패널 → 스텝 반영
_config.Steps = new List<IVisionStep>(PipelineSteps);  // PipelineSteps → _config
```

`SwitchToPipeline`, `btnRunAll`, `btnOK`, 저장 버튼 직전에 반드시 호출됨.

### 파이프라인 복제 (btnDupePl_Click)

스텝을 단순 참조 복사하지 않고 `IStepSerializable`을 활용해 **딥 카피**:

```csharp
var el = new XElement("Step");
srcSerial.SaveParams(el);   // 원본 파라미터 → XML
dstSerial.LoadParams(el);   // XML → 복사본 파라미터
```

---

## 이미지 타입 호환성 검사

```csharp
private static bool IsCompatible(ImageType prevOutput, ImageType nextInput)
{
    if (prevOutput == ImageType.Any || nextInput == ImageType.Any) return true;
    return prevOutput == nextInput;
}
```

- **스텝 추가 시**: 타입 불일치이면 Yes/No 경고 → 강제 추가 가능
- **lstPipeline 표시**: 불일치 스텝 옆에 `"!! Any->Grey 불일치"` 문자 표시
- **OK 버튼**: 불일치 스텝 쌍을 열거 후 Yes/No 확인

---

## 파라미터 패널 관리

### ShowParamPanel(stepIdx)

```
FlushCurrentPanel()                    // 이전 패널 값 → 이전 스텝 반영
ClearParamPanel()
StepParamPanelFactory.Create(step)     // 해당 스텝의 UserControl 생성
panel.BindStep(step)                   // 스텝 값 → UI
grpStepParams.Controls.Add(ctrl)
```

`CogBlobParamPanel`에는 `PreviewRequested` 이벤트 후킹:

```csharp
blobPanel.PreviewRequested += async (s, ev) =>
    await RunStepTestAsync(_currentPanelStepIdx);
```

Blob 패널 내 미리보기 버튼 → 단일 스텝 테스트 자동 실행.

### FlushCurrentPanel()

`_currentPanel.FlushStep(PipelineSteps[_currentPanelStepIdx])` — UI → 스텝 반영.
패널 전환, 스텝 추가/이동/전체실행/저장 직전에 항상 호출됨.

---

## Region 설정 (btnSetTestRegion_Click)

`IRegionStep`인 스텝 선택 시 활성화되는 버튼.

**동작 흐름:**
1. 선택 스텝 앞의 스텝들을 `Task.Run`으로 선실행 → `stepInput` 이미지 확보
2. `cogTestDisplay`에 `stepInput` 표시
3. 기존 `step.Region` 값 또는 이미지 크기 기준 기본값으로 `CogRectangleAffine` 생성
4. `Interactive = true`, `GraphicDOFEnable = All`, Color = Cyan
5. `cogTestDisplay.InteractiveGraphics.Add(_testRegion, "test_region", false)`
6. `step.Region = _testRegion` — Region 참조 할당 (드래그 즉시 반영)

---

## 단일 스텝 테스트 (RunStepTestAsync)

```csharp
// 선행 스텝 실행 (IInspectionStep 건너뜀 — 이미지 변환만 통과)
for (int i = 0; i < idx; i++)
{
    if (!selectedIsInspection && PipelineSteps[i] is IInspectionStep) continue;
    PipelineSteps[i].Execute(context);
}
// 선택 스텝 실행
PipelineSteps[idx].Execute(context);
```

- `selectedIsInspection`: 선택 스텝이 검사 스텝이면 이전 검사 스텝도 실행 (Caliper→Distance 등 의존 관계 고려)
- `_previewRunning` 플래그로 중복 실행 방지

결과 표시: `ShowTestResult(context, step)` — Caliper/Blob/Distance 키별 분기, `DisplayHelper` 활용.

---

## 전체 파이프라인 실행 (btnRunAll_Click)

모든 스텝을 순서대로 실행하며 각 스텝의 **새로 추가된 Data 키**를 추적:

```csharp
var keysBefore = new HashSet<string>(context.Data.Keys);
step.Execute(context);
var newKeys = context.Data.Keys.Where(k => !keysBefore.Contains(k)).ToList();
```

→ 결과 텍스트에 스텝별 성공/실패 + 신규 키 + 결과값 표시
→ `ContinueOnFailure == false`인 스텝 실패 시 이후 스텝 건너뜀 (미실행 목록 출력)

---

## 저장 흐름

| 메서드 | 동작 |
|---|---|
| `CommitCurrentSteps()` | FlushPanel → `_config.Steps` 갱신 (메모리) |
| `CommitAndSave()` | CommitCurrentSteps → `_pipelineManager.SaveAll()` (디스크) |
| `btnSaveStepParams_Click` | CommitAndSave → 결과 텍스트에 저장 완료 표시 |
| `btnSavePipeline_Click` | CommitAndSave → 파이프라인 전체 저장 |
| `btnOK_Click` | 타입 경고 확인 → CommitCurrentSteps → `DialogResult.OK` |

`btnOK`는 디스크 저장을 하지 않는다. 호출자(`PipelineController.ShowEditor()`)가 OK 후 `Save()`를 호출해야 함.

---

## 공개 속성

```csharp
public List<IVisionStep> PipelineSteps { get; private set; }
public int SelectedPipelineIndex => _currentPipelineIdx;
```

`PipelineController`가 `DialogResult.OK` 후 이 속성을 읽어 `ActivePipelineIndex`와 스텝 목록을 갱신.

---

## 주의사항

- `PipelineSteps`는 `_config.Steps`의 얕은 복사 → 스텝 인스턴스 자체는 공유됨
- `CommitCurrentSteps()` 호출 없이 파이프라인을 전환하면 편집 내용이 `_config`에 반영되지 않을 수 있음
- `btnSetTestRegion`에서 `step.Region = _testRegion`으로 직접 할당하므로 테스트 중 드래그한 Region이 실제 스텝에 즉시 적용됨
- `InitTestDisplay()`가 Designer가 아닌 코드로 CogDisplay를 생성 — 디자이너에서 보이지 않음
