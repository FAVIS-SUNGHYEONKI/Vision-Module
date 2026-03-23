# CogStepBase.cs 분석

## 역할

VisionPro 기반 스텝의 **추상 기반 클래스**다.
`IVisionStep`과 `IImageTypedStep`을 구현하며, 자동 이미지 변환과 `DisplayName` 관리를 담당한다.

---

## 클래스 구조

```
IVisionStep ──┐
IImageTypedStep ─┤
               └─▶ CogStepBase (abstract)
                       ├── CogCaliperStep
                       ├── CogBlobStep
                       ├── CogConvertGray
                       ├── CogWeightedRGBStep
                       └── CogCaliperDistanceStep
```

---

## 주요 멤버

| 멤버 | 설명 |
|------|------|
| `abstract Name` | 하위 클래스에서 반드시 구현. XML `type` 속성 키 |
| `DisplayName` | 사용자 지정 표시 이름. 미설정 시 `Name` 반환. `_displayName` 필드로 관리 |
| `ContinueOnFailure` | 기본 `false`. 하위 클래스에서 override 가능 |
| `RequiredInputType` | 기본 `Any`. 필요 시 override (예: CogBlobStep → Grey) |
| `ProducedOutputType` | 기본 `Any`. 이미지 변환 스텝은 override (예: CogConvertGray → Grey) |
| `Execute(context)` | 자동 변환 후 `ExecuteCore` 호출 |
| `abstract ExecuteCore(context)` | 실제 처리 로직. 하위 클래스 구현 |

---

## 자동 이미지 변환 로직 (`Execute`)

```csharp
public void Execute(VisionContext context)
{
    // Mat → CogImage8Grey 자동 변환 (CogImage 없을 때만)
    if (context.CogImage == null && context.MatImage != null)
    {
        context.CogImage = ImageConverter.ToCogImage8Grey(context.MatImage);
        context.MatImage.Dispose();
        context.MatImage = null;
    }
    ExecuteCore(context);
}
```

OpenCV 스텝 이후에 VisionPro 스텝이 바로 연결될 수 있다.

---

## DisplayName 구현

```csharp
private string _displayName;
public string DisplayName
{
    get => string.IsNullOrEmpty(_displayName) ? Name : _displayName;
    set => _displayName = value;
}
```

`IVisionStep.DisplayName` 계약을 이 클래스에서 구현한다.
`CvStepBase`도 동일한 패턴으로 구현되어 있다.
