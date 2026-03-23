# CogCaliperStep.cs 분석

## 역할

VisionPro `CogCaliperTool`을 래핑하여 **에지(밝기 경계)를 검출**하는 스텝이다.
`CogStepBase`를 상속하며 `IStepSerializable`, `IRegionStep`, `IInspectionStep`을 추가 구현한다.

---

## 클래스 계층

```
IVisionStep ──┐
IImageTypedStep ┤
               └─▶ CogStepBase
                       └─▶ CogCaliperStep : IStepSerializable, IRegionStep, IInspectionStep
```

---

## CaliperSelectionMode 열거형

```csharp
public enum CaliperSelectionMode
{
    All,        // 모든 에지 반환
    FirstEdge,  // 스캔 방향 기준 첫 번째 에지 (기본값)
    BestEdge,   // 대비(Contrast) 점수 가장 높은 에지
}
```

---

## 주요 프로퍼티

| 프로퍼티 | 설명 |
|----------|------|
| `Name` | `"VisionPro.Caliper"` |
| `Tool` | 내부 `CogCaliperTool` 인스턴스 (외부 주입 가능) |
| `RunParams` | `_tool.RunParams` — ContrastThreshold, EdgeMode 등 |
| `Region` | `_tool.Region` (CogRectangleAffine). null이면 전체 이미지 스캔 |
| `RegionRequired` | `false` (Region 없이도 실행 가능) |
| `SelectionMode` | `CaliperSelectionMode`. 기본값 `FirstEdge` |

---

## ExecuteCore 동작

```csharp
protected override void ExecuteCore(VisionContext context)
{
    _tool.RunParams.SingleEdgeScorers.Clear();
    int savedMaxResults = _tool.RunParams.MaxResults;

    if (SelectionMode == CaliperSelectionMode.FirstEdge)
    {
        // CogCaliperScorerPositionNeg: Position 작을수록 높은 점수 → 첫 에지 선택
        _tool.RunParams.SingleEdgeScorers.Add(_firstEdgeScorer);
        _tool.RunParams.MaxResults = 1;
    }
    else if (SelectionMode == CaliperSelectionMode.BestEdge)
    {
        // CogCaliperScorerContrast: 대비가 강할수록 높은 점수 → 최고 품질 에지 선택
        _tool.RunParams.SingleEdgeScorers.Add(_bestEdgeScorer);
        _tool.RunParams.MaxResults = 1;
    }
    // All이면 scorer 없음, MaxResults는 사용자 설정 그대로

    _tool.InputImage = context.CogImage;
    _tool.Run();
    _tool.RunParams.MaxResults = savedMaxResults; // 원복

    if (_tool.Results != null && _tool.Results.Count > 0)
    {
        int idx = 0;
        while (context.Data.ContainsKey(Name + "." + idx)) idx++;
        context.Data[Name + "." + idx] = _tool.Results;
        if (Region != null)
            context.Data[Name + "." + idx + ".Region"] = Region;
    }
    else
        context.SetError($"{Name}: Caliper 결과 없음.");
}
```

- Scorer 방식을 사용하므로 VisionPro 내부 평가 로직이 에지 선택에 활용됨
- `MaxResults`를 실행 전 `1`로 강제 설정하고 실행 후 원복 (SelectionMode가 바뀌어도 사용자 설정 유지)
- `Region`도 컨텍스트에 같이 저장하여 `CogCaliperDistanceStep`이 2D 좌표 변환 시 활용

---

## XML 직렬화 (IStepSerializable)

**저장 필드:**

| 필드 | XML 경로 |
|------|---------|
| Region (CenterX, CenterY, SideXLength, SideYLength, Rotation, Skew) | `<Region>` |
| ContrastThreshold | `<RunParams><ContrastThreshold>` |
| EdgeMode | `<RunParams><EdgeMode>` |
| Edge0Polarity | `<RunParams><Edge0Polarity>` |
| FilterHalfSizeInPixels | `<RunParams><FilterHalfSizeInPixels>` |
| MaxResults | `<RunParams><MaxResults>` |
| SelectionMode | `<RunParams><SelectionMode>` |

실수값은 `InvariantCulture`로 직렬화하여 로케일 독립성 보장.
