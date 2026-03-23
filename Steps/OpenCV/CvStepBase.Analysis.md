# CvStepBase.cs 분석

## 역할

OpenCV 기반 스텝의 **추상 기반 클래스**다.
`IVisionStep`과 `IImageTypedStep`을 구현하며, 자동 이미지 변환과 `DisplayName` 관리를 담당한다.

---

## 클래스 구조

```
IVisionStep ──┐
IImageTypedStep ─┤
               └─▶ CvStepBase (abstract)
                       └── CvThresholdStep
```

---

## 주요 멤버

| 멤버 | 설명 |
|------|------|
| `abstract Name` | 하위 클래스에서 반드시 구현 |
| `DisplayName` | 사용자 지정 표시 이름. 미설정 시 `Name` 반환 |
| `ContinueOnFailure` | 기본 `false` |
| `RequiredInputType` | 기본 `Any` (자동 변환으로 그레이/컬러 모두 허용) |
| `ProducedOutputType` | 기본 `Grey` (OpenCV 스텝은 기본적으로 그레이 Mat 출력) |
| `Execute(context)` | 자동 변환 후 `ExecuteCore` 호출 |
| `abstract ExecuteCore(context)` | 실제 처리 로직. 하위 클래스 구현 |

---

## 자동 이미지 변환 로직 (`Execute`)

```csharp
public void Execute(VisionContext context)
{
    // CogImage → Mat 자동 변환 (MatImage 없을 때만)
    if (context.MatImage == null && context.CogImage != null)
    {
        context.MatImage = ImageConverter.ToMat(context.CogImage);
        (context.CogImage as IDisposable)?.Dispose();
        context.CogImage = null;
    }
    ExecuteCore(context);
}
```

VisionPro 스텝 이후에 OpenCV 스텝이 바로 연결될 수 있다.

---

## DisplayName 구현

`CogStepBase`와 동일한 패턴:
```csharp
private string _displayName;
public string DisplayName
{
    get => string.IsNullOrEmpty(_displayName) ? Name : _displayName;
    set => _displayName = value;
}
```

`IVisionStep.DisplayName` 계약을 이 클래스에서 구현한다.
