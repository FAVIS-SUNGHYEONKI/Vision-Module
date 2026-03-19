using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Cognex.VisionPro;
using OpenCvSharp;

namespace Vision
{
    /// <summary>
    /// 파이프라인 전체에서 공유되는 이미지 및 결과 컨텍스트.
    ///
    /// 각 스텝은 이 객체를 통해 이미지를 받고, 처리 결과를 다음 스텝으로 전달합니다.
    /// CogImage / MatImage 중 하나만 설정해도 각 스텝 기반 클래스(CogStepBase / CvStepBase)가
    /// 실행 시점에 필요한 포맷으로 자동 변환합니다.
    ///
    /// 사용 패턴:
    ///   var context = new VisionContext { CogImage = loadedImage };
    ///   await pipeline.RunAsync(context);
    ///   var result = context.Data["VisionPro.Caliper.0"] as CogCaliperResults;
    /// </summary>
    public class VisionContext : IDisposable
    {
        /// <summary>
        /// OpenCV Mat 포맷 이미지.
        /// CvStepBase 계열 스텝이 사용하며, CogImage가 있을 경우 자동 변환됩니다.
        /// </summary>
        public Mat       MatImage { get; set; }

        /// <summary>
        /// Cognex VisionPro ICogImage 포맷 이미지.
        /// CogStepBase 계열 스텝이 사용하며, MatImage가 있을 경우 자동 변환됩니다.
        /// </summary>
        public ICogImage CogImage { get; set; }

        /// <summary>
        /// 파이프라인 전체 성공 여부.
        /// 어느 스텝이라도 SetError()를 호출하면 false로 전환됩니다.
        /// ContinueOnFailure = true 인 스텝은 false 상태에서도 다음 스텝이 실행됩니다.
        /// </summary>
        public bool IsSuccess { get; set; } = true;

        /// <summary>
        /// 스텝별 오류 메시지 누적 목록.
        /// SetError() 호출 시 메시지가 추가됩니다.
        /// </summary>
        public List<string> Errors { get; } = new List<string>();

        /// <summary>
        /// 스텝 간 검사 결과 전달용 딕셔너리.
        ///
        /// 키 규칙:
        ///   "VisionPro.Caliper.0", "VisionPro.Caliper.1", ... → CogCaliperResults
        ///   "VisionPro.Blob"                                  → CogBlobResults
        ///
        /// 동일 타입의 스텝이 여러 번 실행되면 인덱스 번호가 증가합니다(Caliper의 경우).
        /// </summary>
        public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();

        /// <summary>
        /// 스텝 실행 실패를 기록합니다.
        /// IsSuccess를 false로 설정하고 오류 메시지를 Errors에 추가합니다.
        /// </summary>
        /// <param name="message">오류 설명 문자열</param>
        public void SetError(string message)
        {
            IsSuccess = false;
            Errors.Add(message);
        }

        /// <summary>
        /// 보유 중인 이미지 리소스(MatImage, CogImage)를 해제합니다.
        /// </summary>
        public void Dispose()
        {
            MatImage?.Dispose();
            MatImage = null;
            (CogImage as IDisposable)?.Dispose();
            CogImage = null;
        }
    }

    /// <summary>
    /// 모든 Vision 스텝이 구현해야 하는 핵심 계약 인터페이스.
    ///
    /// 구현 책임:
    ///   - Name             : 스텝 식별자 (XML 저장 키로도 사용)
    ///   - ContinueOnFailure: 실패 후 파이프라인 계속 여부
    ///   - Execute()        : 실제 처리 로직
    ///
    /// 선택적 확장 인터페이스:
    ///   IStepSerializable  — XML 저장/로드
    ///   IRegionStep        — 검사 영역(ROI) 보유
    ///   IInspectionStep    — 검사 결과를 Data에 기록하는 스텝 표시
    ///   IImageTypedStep    — 입출력 이미지 타입 선언
    /// </summary>
    public interface IVisionStep
    {
        /// <summary>
        /// 스텝의 고유 이름. PipelineManager가 XML 저장/로드 시 type 속성으로 사용합니다.
        /// 예: "VisionPro.Caliper", "VisionPro.Blob", "OpenCV.Threshold"
        /// </summary>
        string Name { get; }

        /// <summary>
        /// true이면 이 스텝이 실패(context.IsSuccess = false)해도 다음 스텝을 계속 실행합니다.
        /// false이면 실패 즉시 파이프라인이 중단됩니다.
        /// </summary>
        bool ContinueOnFailure { get; }

        /// <summary>
        /// 스텝의 처리를 실행합니다. 결과는 context에 기록됩니다.
        /// 오류 발생 시 context.SetError()를 호출해야 합니다(예외 throw 지양).
        /// </summary>
        /// <param name="context">파이프라인 공유 컨텍스트</param>
        void Execute(VisionContext context);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 이미지 타입 호환성 시스템
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 스텝의 파라미터를 XML 요소로 저장/복원하는 선택적 인터페이스.
    ///
    /// PipelineManager.SaveAll() 이 각 스텝에 대해 SaveParams()를 호출하고,
    /// LoadAll() 이 LoadParams()를 호출합니다.
    /// XElement el은 해당 요소이며, 하위 요소를 자유롭게 추가할 수 있습니다.
    /// </summary>
    public interface IStepSerializable
    {
        /// <summary>스텝 파라미터를 XML 요소에 기록합니다.</summary>
        /// <param name="el">쓰기 대상 XElement</param>
        void SaveParams(XElement el);

        /// <summary>XML 요소에서 스텝 파라미터를 복원합니다.</summary>
        /// <param name="el">읽기 대상 XElement</param>
        void LoadParams(XElement el);
    }

    /// <summary>
    /// 검사 영역(CogRectangleAffine ROI)을 보유하는 스텝이 구현하는 인터페이스.
    ///
    /// GUI는 이 인터페이스를 통해 스텝마다 독립적인 Region을 설정/표시할 수 있습니다.
    /// Region = null 이고 RegionRequired = false 이면 전체 이미지를 대상으로 실행합니다.
    /// </summary>
    public interface IRegionStep
    {
        /// <summary>검사 영역(ROI). null이면 전체 이미지가 대상입니다.</summary>
        CogRectangleAffine Region { get; set; }

        /// <summary>
        /// Region이 반드시 필요하면 true, 없어도 동작 가능하면 false.
        /// VisionForm은 true인 스텝에 Region이 없으면 실행 전 경고를 표시합니다.
        /// </summary>
        bool RegionRequired { get; }
    }

    /// <summary>
    /// 이미지를 분석하여 결과를 VisionContext.Data에 기록하는 검사 스텝의 마커 인터페이스.
    ///
    /// 이 인터페이스를 구현한 스텝은 PipelineEditorForm의 단일 스텝 테스트 시,
    /// 선택된 스텝 이전 위치에서는 실행되지 않습니다.
    /// (이미지 변환 스텝만 선행 실행되어 정확한 단일 스텝 결과를 확인할 수 있습니다.)
    ///
    /// 구현체: CogCaliperStep, CogBlobStep
    /// </summary>
    public interface IInspectionStep { }

    /// <summary>
    /// 스텝의 입출력 이미지 타입을 선언하는 선택적 인터페이스.
    ///
    /// PipelineEditorForm이 스텝을 추가하거나 순서를 변경할 때
    /// 인접 스텝 간 타입 불일치를 감지하여 시각적 경고를 표시합니다.
    /// 이 인터페이스를 구현하지 않는 스텝은 Any→Any로 취급합니다.
    /// </summary>
    public interface IImageTypedStep
    {
        /// <summary>이 스텝이 입력으로 받아야 하는 이미지 타입.</summary>
        ImageType RequiredInputType  { get; }

        /// <summary>이 스텝이 실행 후 context에 남기는 이미지 타입.</summary>
        ImageType ProducedOutputType { get; }
    }

    /// <summary>
    /// 파이프라인 스텝이 처리하는 이미지의 채널 종류.
    ///
    /// 타입 호환 규칙:
    ///   Any  ← Any, Grey, Color  (항상 호환)
    ///   Grey ← Grey 만 호환
    ///   Color← Color 만 호환
    /// </summary>
    public enum ImageType
    {
        /// <summary>제약 없음 — 어떤 이미지도 허용/출력합니다.</summary>
        Any,
        /// <summary>그레이스케일 — CogImage8Grey / CV_8UC1.</summary>
        Grey,
        /// <summary>컬러 RGB — CogImage24PlanarColor / CV_8UC3.</summary>
        Color,
    }
}
