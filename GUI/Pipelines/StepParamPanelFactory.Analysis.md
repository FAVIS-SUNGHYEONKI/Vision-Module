# StepParamPanelFactory.cs 분석

## 역할

스텝 이름으로 파라미터 편집 패널(UserControl)을 생성하는 **정적 팩토리**다.

```csharp
namespace Vision.UI

public static class StepParamPanelFactory
```

---

## Create

```csharp
public static Control Create(IVisionStep step)
{
    switch (step?.Name)
    {
        case "VisionPro.Caliper":         return new CogCaliperParamPanel();
        case "VisionPro.Blob":            return new CogBlobParamPanel();
        case "VisionPro.ConvertGray":     return new CogConvertGrayParamPanel();
        case "VisionPro.CaliperDistance": return new CogCaliperDistanceParamPanel();
        default:                          return null;
    }
}
```

- `step?.Name` — null 안전 처리
- 해당 패널이 없는 스텝(예: `CvThresholdStep`)은 `null` 반환

---

## 반환값 활용

`PipelineController.GetParamPanel()`에서 호출 후 `BindStep()`을 자동으로 호출한다:

```csharp
// PipelineController
public Control GetParamPanel(IVisionStep step)
{
    var ctrl = StepParamPanelFactory.Create(step);
    (ctrl as IStepParamPanel)?.BindStep(step);
    return ctrl;
}
```

반환된 `Control`은 `IStepParamPanel`도 구현하므로 두 가지로 사용 가능하다:
- `Control`로 → 폼에 `Controls.Add()`
- `IStepParamPanel`로 캐스팅 → `FlushStep()` 호출

---

## 새 스텝 추가 시 절차

1. `IStepParamPanel`을 구현한 UserControl 작성
2. 이 팩토리에 `case` 하나 추가
3. 끝 — 나머지(편집기 표시, 외부 앱)는 자동으로 연결됨
