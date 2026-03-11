# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 프로젝트 개요

- **타입**: C# 클래스 라이브러리 (`OutputType: Library`)
- **대상 프레임워크**: .NET Framework 4.8
- **플랫폼**: x64 (Cognex VisionPro가 AMD64 전용)
- **출력**: `Vision.dll`

## 빌드 명령

Visual Studio 솔루션 파일(`Vision.sln`)을 사용하는 것이 권장됩니다.

```bash
# Debug 빌드
msbuild Vision.sln /p:Configuration=Debug /p:Platform=x64

# Release 빌드
msbuild Vision.sln /p:Configuration=Release /p:Platform=x64
```

출력 경로:
- Debug: `bin\x64\Debug\Vision.dll`
- Release: `bin\x64\Release\Vision.dll`

## 외부 의존성

| 패키지 | 버전 | 비고 |
|--------|------|------|
| Cognex VisionPro | 71.2.0.0 | `C:\Program Files\Cognex\VisionPro\ReferencedAssemblies\`에 설치 필요 |
| OpenCvSharp4 | 4.13.0 | NuGet (`packages/`) |

Cognex VisionPro는 별도 라이센스 설치가 필요합니다. 미설치 시 빌드 실패합니다.

## 아키텍처

### 핵심 구조 (파이프라인 패턴)

```
VisionContext (공유 데이터)
    └─ VisionPipeline
         ├─ IVisionStep (인터페이스)
         │    ├─ CvStepBase  → OpenCV 기반 스텝 추상 클래스
         │    │    └─ CvThresholdStep
         │    └─ CogStepBase → VisionPro 기반 스텝 추상 클래스
         │         └─ CogCaliperStep
         └─ ImageConverter (Mat ↔ ICogImage 변환)
```

### 주요 클래스 역할

**`VisionContext`** (`Core.cs`)
- 파이프라인 전체에서 공유되는 컨텍스트 객체
- `MatImage` (OpenCV Mat)와 `CogImage` (Cognex ICogImage)를 동시 보유 가능
- `Data` 딕셔너리로 스텝 간 결과 전달
- `IsSuccess` / `Errors`로 검사 결과 집계

**`IVisionStep`** (`Core.cs`)
- 모든 스텝의 공통 인터페이스
- `ContinueOnFailure`: `false`면 실패 시 파이프라인 중단, `true`면 계속 진행

**`VisionPipeline`** (`PipeLine.cs`)
- 스텝을 순차 실행하는 비동기 파이프라인 (`RunAsync`)
- 메서드 체이닝: `.AddStep().AddStep()...`
- 이벤트: `OnStepStarted`, `OnStepFailed`, `OnPipelineFinished`

**`CvStepBase` / `CogStepBase`** (`Steps/`)
- 각각 OpenCV, VisionPro 기반 스텝의 추상 기반 클래스
- **자동 이미지 변환**: `CvStepBase`는 `CogImage`만 있으면 자동으로 `Mat`으로 변환하고 `ExecuteCore` 호출. `CogStepBase`는 반대 방향으로 동작.
- 혼합 파이프라인(OpenCV ↔ VisionPro 스텝 교차)이 자연스럽게 가능

**`ImageConverter`** (`Converters/ImageConverter.cs`)
- `Mat → CogImage8Grey`, `ICogImage → Mat` 변환 유틸리티
- 컬러 Mat은 그레이스케일로 자동 변환

### 이미지 동기화 규칙

스텝에서 이미지를 수정할 경우 다른 포맷을 `null`로 초기화해야 합니다:
```csharp
context.MatImage = result;
context.CogImage = null; // CvImage 교체 후 VpImage는 무효
```
다음 스텝의 기반 클래스(`CvStepBase`/`CogStepBase`)가 자동으로 재변환합니다.

## 새 스텝 추가 방법

### OpenCV 스텝
```csharp
public class MyOpenCvStep : CvStepBase
{
    public override string Name => "OpenCV.MyStep";

    protected override void ExecuteCore(VisionContext context)
    {
        // context.MatImage 사용 (null 보장됨)
        // 이미지 교체 시 context.CogImage = null 처리
    }
}
```

### VisionPro 스텝
```csharp
public class MyCogStep : CogStepBase
{
    public override string Name => "VisionPro.MyStep";

    protected override void ExecuteCore(VisionContext context)
    {
        // context.CogImage 사용 (null 보장됨)
        // 결과는 context.Data["VisionPro.MyStep"] 에 저장
    }
}
```

파일은 `Steps/OpenCV/` 또는 `Steps/Cognex/` 디렉토리에 추가하고 `Vision.csproj`의 `<Compile Include="...">` 항목도 추가해야 합니다.
