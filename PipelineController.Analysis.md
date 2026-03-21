# PipelineController.cs 분석

## 역할

외부 앱이 사용하는 **단일 진입점(Facade)** 클래스다.
Vision Module의 모든 기능을 하나의 인터페이스로 통합한다.

```csharp
public class PipelineController
```

---

## 내부 구성

```csharp
private readonly PipelineManager      _manager;
private readonly List<StepDescriptor> _stepDescriptors;
```

- `_manager`: 파이프라인 저장/로드/관리
- `_stepDescriptors`: 편집기 팔레트에 등록된 스텝 유형 목록

---

## 생성자 — 기본 스텝 등록

```csharp
public PipelineController(string configFolder)
{
    _manager = new PipelineManager(configFolder);
    RegisterBuiltinSteps();
}
```

기본 제공 스텝:

| 표시 이름 | 카테고리 | 스텝 클래스 |
|---|---|---|
| ConvertGray (컬러→회색) | Cognex | `CogConvertGray` |
| Caliper (에지 검출) | Cognex | `CogCaliperStep` |
| Blob (영역 검출) | Cognex | `CogBlobStep` |
| CaliperDistance (거리 측정) | Cognex | `CogCaliperDistanceStep` |
| Threshold (이진화) | OpenCV | `CvThresholdStep` |

`RegisterStep()`으로 커스텀 스텝을 추가할 수 있다.

---

## ShowEditor

```csharp
public DialogResult ShowEditor(IWin32Window owner = null, ICogImage inputImage = null)
```

- `EnsureActivePipeline()`: 파이프라인이 없으면 "기본 파이프라인" 자동 생성
- `PipelineEditorForm`을 모달로 열고 OK 시 활성 인덱스를 갱신

---

## RunAsync

```csharp
public async Task<VisionResult> RunAsync(ICogImage image)
```

실행 흐름:
```
1. 활성 파이프라인이 없거나 스텝이 없으면 → VisionResult.Empty 반환

2. PipelineConfig.Steps → VisionPipeline에 AddStep()

3. using (var ctx = new VisionContext { CogImage = image })
    await vp.RunAsync(ctx)
    return VisionResult.FromContext(ctx, pipeline.Steps)
```

`using`으로 `VisionContext`를 감싸므로 실행 후 이미지 메모리가 자동 해제된다.

---

## 파라미터 패널 관련 메서드

| 메서드 | 역할 |
|---|---|
| `GetParamPanel(step)` | 스텝에 맞는 UserControl 반환 + BindStep 호출 |
| `ApplyParamPanel(step, panel)` | `panel.FlushStep(step)` — 메모리 반영만 |
| `ApplyAndSave(step, panel)` | `FlushStep()` + `SaveAll()` |

사용 패턴:
```csharp
var ctrl  = controller.GetParamPanel(step);
var panel = (IStepParamPanel)ctrl;
myGroupBox.Controls.Add(ctrl);

// 적용만
controller.ApplyParamPanel(step, panel);

// 적용 + 저장
controller.ApplyAndSave(step, panel);
```

---

## 다중 파이프라인 관리

### DuplicateActivePipeline — 깊은 복사

```csharp
// XML을 중간 매개체로 사용하여 파라미터까지 완전히 복사
var el = new XElement("Step");
srcSerial.SaveParams(el);
dstSerial.LoadParams(el);
```

직접 참조를 복사하지 않고 XML 직렬화/역직렬화를 거쳐 완전히 독립된 복사본을 만든다.

### RemovePipeline

```csharp
public bool RemovePipeline(int index)
{
    if (_manager.Configs.Count <= 1) return false;  // 마지막 하나는 삭제 불가
    _manager.RemoveAt(index);
    return true;
}
```

---

## 전체 API 요약

```csharp
// 초기화
var controller = new PipelineController(@"C:\config");
controller.Load();

// 편집
controller.ShowEditor(this, image);
controller.Save();

// 실행
var result = await controller.RunAsync(image);

// 결과 표시
controller.DrawResults(display, result);

// 파라미터 편집 (외부 앱)
var ctrl = controller.GetParamPanel(step);
controller.ApplyAndSave(step, (IStepParamPanel)ctrl);

// 파이프라인 관리
controller.AddPipeline("새 파이프라인");
controller.DuplicateActivePipeline();
controller.RemovePipeline(index);
controller.RenamePipeline(index, "새 이름");
```
