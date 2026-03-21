# CogCaliperStep.cs 분석

## 역할

VisionPro `CogCaliperTool`을 래핑하여 **에지(밝기 경계)를 검출**하는 스텝이다.

```csharp
public class CogCaliperStep : CogStepBase, IStepSerializable, IRegionStep, IInspectionStep
```

---

## 주요 속성

| 속성 | 타입 | 역할 |
|---|---|---|
| `Name` | `string` | `"VisionPro.Caliper"` (결과 키 접두어 겸 XML type) |
| `Region` | `CogRectangleAffine` | 스캔 영역. null이면 전체 이미지 |
| `RunParams` | `CogCaliper` | 에지 검출 파라미터 |
| `Tool` | `CogCaliperTool` | 내부 툴 인스턴스 (외부 주입 가능) |
| `RegionRequired` | `bool` | `false` — Region 없이 전체 이미지 스캔 가능 |

---

## ExecuteCore — 실행

```csharp
protected override void ExecuteCore(VisionContext context)
{
    _tool.InputImage = context.CogImage;
    _tool.Run();

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

### 키 자동 증가 패턴

동일 파이프라인에 Caliper 스텝이 여러 개 있을 때 충돌 방지:
```
첫 번째 스텝 → "VisionPro.Caliper.0"
두 번째 스텝 → "VisionPro.Caliper.1"
```

### Region도 함께 저장하는 이유

`CogCaliperDistanceStep`이 1D Position → 2D 이미지 좌표 변환 시 Region의 중심점과 회전각이 필요하다.

```
"VisionPro.Caliper.0"        → CogCaliperResults
"VisionPro.Caliper.0.Region" → CogRectangleAffine
```

---

## XML 직렬화 (IStepSerializable)

### 저장 필드

**Region:**
- `CenterX`, `CenterY`, `SideXLength`, `SideYLength`, `Rotation`, `Skew`

**RunParams:**
- `ContrastThreshold`, `EdgeMode`, `Edge0Polarity`, `FilterHalfSizeInPixels`, `MaxResults`

### InvariantCulture 사용 이유

```csharp
v.ToString("R", CultureInfo.InvariantCulture)
```

로케일에 따라 소수점 기호가 `.` 또는 `,`로 달라질 수 있다.
`InvariantCulture`로 항상 `.`을 사용하여 파일 이식성을 보장한다.

### 로드 시 기본값

누락된 XML 요소는 기본값으로 대체된다:
```
ContrastThreshold = 15.0
MaxResults        = 1
EdgeMode          = 0 (Single)
```

---

## XML 헬퍼 메서드

```csharp
Xd(name, double)  // double → XElement (InvariantCulture)
Xi(name, int)     // int    → XElement
Rd(el, name, def) // XElement → double (실패 시 def)
Ri(el, name, def) // XElement → int    (실패 시 def)
```

이 패턴은 `CogBlobStep`, `CvThresholdStep` 등 모든 직렬화 스텝에서 동일하게 사용한다.
