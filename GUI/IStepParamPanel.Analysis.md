# IStepParamPanel.cs 분석

## 역할

각 스텝의 파라미터를 편집하는 **UserControl이 구현하는 인터페이스**다.

```csharp
namespace Vision.UI

public interface IStepParamPanel
{
    void BindStep(IVisionStep step);
    void FlushStep(IVisionStep step);
}
```

---

## 메서드

| 메서드 | 방향 | 역할 |
|---|---|---|
| `BindStep(step)` | 스텝 → UI | 스텝의 현재 파라미터 값을 UI 컨트롤에 반영 |
| `FlushStep(step)` | UI → 스텝 | UI 컨트롤의 현재 값을 스텝 파라미터에 기록 |

---

## 사용 위치

- `PipelineEditorForm` 내부 — 스텝 선택 시 패널 로드
- 외부 앱 — `PipelineController.GetParamPanel()`로 받아서 직접 사용

두 곳에서 **동일한 인터페이스**로 사용된다.

---

## 외부 앱 사용 패턴

```csharp
// 패널 가져오기
var ctrl  = controller.GetParamPanel(step);   // Control 반환
var panel = (IStepParamPanel)ctrl;

// 폼에 추가
ctrl.Dock = DockStyle.Fill;
myGroupBox.Controls.Add(ctrl);

// 스텝 전환 시 BindStep은 GetParamPanel 내부에서 자동 호출됨

// 적용 (저장 없음)
controller.ApplyParamPanel(step, panel);  // → panel.FlushStep(step)

// 적용 + 저장
controller.ApplyAndSave(step, panel);     // → FlushStep + SaveAll
```

---

## 구현 클래스 목록

| 클래스 | 담당 스텝 |
|---|---|
| `CogCaliperParamPanel` | `CogCaliperStep` |
| `CogBlobParamPanel` | `CogBlobStep` |
| `CogConvertGrayParamPanel` | `CogConvertGray` |
| `CogCaliperDistanceParamPanel` | `CogCaliperDistanceStep` |
| `CvThresholdParamPanel` | `CvThresholdStep` |

새 스텝을 추가할 때 이 인터페이스를 구현한 UserControl을 만들고 `StepParamPanelFactory`에 등록한다.
