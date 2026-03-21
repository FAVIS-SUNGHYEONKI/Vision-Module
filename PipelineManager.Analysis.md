# PipelineManager.cs 분석

## 역할

여러 `PipelineConfig`를 관리하고 `pipelines.xml`로 저장/로드하는 관리자다.

```csharp
public class PipelineManager
```

---

## 상태

```csharp
private readonly List<PipelineConfig> _configs;
private int _activeIndex;
private readonly string _folder;
```

`ActiveIndex`는 setter에서 자동 클램핑된다:
```csharp
set => _activeIndex = Math.Max(0, Math.Min(value, Math.Max(0, _configs.Count - 1)));
```
음수나 범위 초과 값을 넣어도 항상 유효한 인덱스가 된다.

---

## 추가 / 제거

```csharp
public void Add(PipelineConfig config)
{
    _configs.Add(config);
    _activeIndex = _configs.Count - 1;  // 새 항목으로 활성화
}

public void RemoveAt(int index)
{
    _configs.RemoveAt(index);
    _activeIndex = Math.Max(0, Math.Min(_activeIndex, _configs.Count - 1));
}
```

추가 시 자동으로 해당 항목이 활성화된다.
삭제 후 인덱스가 범위를 벗어나면 자동 조정된다.

---

## SaveAll — XML 저장

```csharp
public void SaveAll()
```

저장 흐름:
```
Directory.CreateDirectory(_folder)

<Pipelines active="0">
 └─ <Pipeline name="기본">
     └─ <Steps>
         ├─ <Step type="VisionPro.Caliper">
         │   ← IStepSerializable.SaveParams(el) 호출
         └─ <Step type="VisionPro.Blob">
             ← IStepSerializable.SaveParams(el) 호출
```

`IStepSerializable`을 구현하지 않는 스텝은 `type` 속성만 저장된다.
`XDocument.Save(FilePath)`로 UTF-8 XML 파일을 생성한다.

---

## LoadAll — XML 로드

```csharp
public void LoadAll(IReadOnlyDictionary<string, Func<IVisionStep>> stepFactories)
```

### stepFactories 주입 이유

Vision.dll이 GUI 어셈블리나 외부 스텝에 **의존하지 않도록** 의존성을 역전한 것이다.
로드 시 스텝을 어떻게 생성할지는 외부(`PipelineController`)가 전달한다.

```
{ "VisionPro.Caliper" => () => new CogCaliperStep() }
{ "VisionPro.Blob"    => () => new CogBlobStep()    }
```

### 로드 흐름

```
XDocument.Load(FilePath)
 └─ <Pipelines active="N">
     └─ <Pipeline name="...">
         └─ <Step type="...">
             ├─ stepFactories[type]() → 스텝 인스턴스 생성
             └─ IStepSerializable.LoadParams(stepEl) → 파라미터 복원
```

알 수 없는 `type`은 조용히 건너뛴다 — 플러그인 제거나 버전 불일치 시 크래시 방지.
파일이 없거나 파싱 실패 시도 조용히 무시한다.

---

## 파일 경로

```csharp
public string FilePath => Path.Combine(_folder, "pipelines.xml");
```

생성자에서 받은 폴더에 고정 파일명으로 저장된다.
