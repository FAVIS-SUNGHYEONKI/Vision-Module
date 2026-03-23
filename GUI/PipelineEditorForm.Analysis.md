# PipelineEditorForm.cs 분석

## 역할

파이프라인 목록 관리 + 스텝 구성 + 파라미터 편집 + 단일/전체 테스트를 하나의 다이얼로그에서 처리하는 폼.
`PipelineController.ShowEditor()`를 통해 다이얼로그로 호출된다.

---

## 핵심 설계 원칙: Staged 편집

편집 중 내용은 실제 `PipelineConfig.Steps`가 아닌 별도 객체에 보관된다.
OK 버튼을 눌러야만 실제 파이프라인에 반영된다.

```
_stagedByPipeline : Dictionary<int, List<IVisionStep>>
    ├── key: 파이프라인 인덱스
    └── value: 편집 중인 스텝 목록 (deep copy)
```

**흐름:**
1. 폼 열기 → `DeepCopySteps(_config.Steps)` → `PipelineSteps` (현재 편집 작업 대상)
2. 파이프라인 전환 → `StashCurrentStaged()` → `SwitchToPipeline(idx)`
3. OK 클릭 → `ApplyAllStagedToConfigs()` → `_pipelineManager.SaveAll()`
4. Cancel → staged 버림, 실제 config 유지

**Deep copy 방법:**
```csharp
private List<IVisionStep> DeepCopySteps(List<IVisionStep> source)
{
    // StepDescriptor.CreateStep()으로 새 인스턴스 생성
    // IStepSerializable SaveParams/LoadParams 경유로 파라미터 복사
}
```

---

## 주요 메서드 목록

### 파이프라인 CRUD
| 메서드 | 설명 |
|--------|------|
| `SwitchToPipeline(idx)` | staged 복원 또는 deep copy로 파이프라인 전환 |
| `StashCurrentStaged()` | 현재 `PipelineSteps`를 `_stagedByPipeline`에 저장 |
| `CommitCurrentToConfig()` | staged → `_config.Steps` (단일 파이프라인 즉시 저장용) |
| `ApplyAllStagedToConfigs()` | 전체 staged → 실제 config (OK 버튼 전용) |
| `btnNewPl_Click` | 새 파이프라인 생성 |
| `btnDupePl_Click` | 현재 staged 기준으로 복제 |
| `btnDeletePl_Click` | 삭제. staged dict 인덱스 재정렬 처리 포함 |

### 스텝 관리
| 메서드 | 설명 |
|--------|------|
| `btnAdd_Click` | 스텝 추가. 이미지 타입 불일치 시 경고 |
| `btnRemove_Click` | 선택 스텝 제거 |
| `lstPipeline_DoubleClick` | 더블클릭으로 스텝 삭제 (`btnRemove_Click` 위임) |
| `btnMoveUp/Down_Click` | 인접 스텝과 위치 교환 |

### 드래그&드롭 순서 변경
```
MouseDown   → _dragFromIdx, _dragStartPoint 저장, _dragPending = true
MouseMove   → DragSize 임계값 초과 시에만 DoDragDrop() 호출
MouseUp     → _dragPending = false (클릭만 할 경우 정리)
DragOver    → 삽입 위치 계산, DragListBox.InsertIndex 업데이트, Invalidate()
DragDrop    → RemoveAt + Insert, RefreshPipelineList()
DragLeave   → InsertIndex = -1, Invalidate()
```

> **중요:** `MouseDown`에서 즉시 `DoDragDrop()`을 호출하면 단순 클릭도 드래그로 처리되어
> `SelectedIndexChanged` → `ShowParamPanel()` 흐름이 방해받는 문제가 있었다.
> `MouseMove`에서 임계값 초과 후 드래그를 시작하도록 수정하여 해결.

### DisplayName 편집
- 스텝 선택 시 `txtStepDisplayName`에 현재 `step.DisplayName` 표시
- `FlushCurrentPanel()`에서 텍스트박스 값을 `PipelineSteps[idx].DisplayName`에 반영
- `RefreshPipelineList()`에서 `DisplayName != Name`이면 `"DisplayName [Name]"` 형태로 목록 표시

### 파라미터 패널
| 메서드 | 설명 |
|--------|------|
| `ShowParamPanel(stepIdx)` | 스텝에 맞는 패널 생성, BindStep, txtStepDisplayName 활성화 |
| `FlushCurrentPanel()` | DisplayName 반영 + `_currentPanel.FlushStep()` |
| `ClearParamPanel()` | 패널 제거, txtStepDisplayName 초기화/비활성화 |

---

## DragListBox (내부 클래스)

`ListBox`를 상속한 커스텀 컨트롤. 드래그 삽입 위치를 파란 선으로 표시.

```csharp
internal class DragListBox : ListBox
{
    private const int WM_PAINT = 0x000F;
    public int InsertIndex { get; set; } = -1;

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WM_PAINT && InsertIndex >= 0)
            DrawInsertLine();
    }
}
```

**설계 이유:** `ControlPaint.DrawReversibleLine`은 XOR 방식이라 ListBox가 리페인트할 때마다 선이 소멸됨.
`WM_PAINT` 이후 `CreateGraphics()`로 매 페인트마다 선을 재그리는 방식으로 안정성 확보.

삽입 선 스타일: DodgerBlue 2px 가로선 + 양 끝 삼각형 마커.

---

## 테스트 기능

### 단일 스텝 테스트 (`RunStepTestAsync`)
- 선택 스텝 이전 스텝들을 순차 실행 (단, `IInspectionStep`인 스텝은 선택 스텝도 `IInspectionStep`일 때만 포함)
- 선택 스텝만 실행 후 결과를 `txtTestResult`에 표시 + `cogTestDisplay`에 그래픽 렌더링

### 전체 파이프라인 테스트 (`btnRunAll_Click`)
- 모든 스텝을 순차 실행
- 각 스텝별 성공/실패, 생성된 결과 키, 오류 메시지를 누적 표시

### Region 설정 (`btnSetTestRegion_Click`)
- 선택 스텝이 `IRegionStep`인 경우만 활성화
- `CogRectangleAffine`을 `cogTestDisplay.InteractiveGraphics`에 추가
- 드래그 조정 후 `[스텝 테스트]` 버튼으로 확인

---

## 저장 흐름

| 상황 | 동작 |
|------|------|
| 스텝 파라미터 저장 버튼 | `CommitCurrentToConfig()` + `SaveAll()` (현재 파이프라인만) |
| OK 버튼 | `ApplyAllStagedToConfigs()` + `SaveAll()` (전체 파이프라인) |
| Cancel 버튼 | staged 버림, 저장 없음 |

> `btnSavePipeline` (파이프라인 순서 저장) 버튼은 제거됨.
> 스텝 순서 변경은 OK 버튼 클릭 시에만 실제 반영된다.
