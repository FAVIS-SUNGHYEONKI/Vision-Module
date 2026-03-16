using System;
using System.Collections.Generic;
using System.Xml.Linq;
using Cognex.VisionPro;
using OpenCvSharp;

namespace Vision
{
    /// <summary>
    /// 파이프라인 전체에서 공유되는 이미지 및 결과 컨텍스트.
    /// CvImage / VpImage 중 하나만 있어도 각 Step 기반 클래스가 자동 변환합니다.
    /// </summary>
    public class VisionContext : IDisposable
    {
        // 이미지 저장소 (두 라이브러리 포맷 동시 보유 가능)
        public Mat      MatImage  { get; set; }
        public ICogImage CogImage { get; set; }

        // 검사 통과 여부
        public bool IsSuccess { get; set; } = true;

        // 스텝별 에러 메시지 누적
        public List<string> Errors { get; } = new List<string>();

        // 스텝 간 데이터 전달용
        public Dictionary<string, object> Data { get; } = new Dictionary<string, object>();

        /// <summary>실패 상태로 설정하고 에러 메시지를 기록합니다.</summary>
        public void SetError(string message)
        {
            IsSuccess = false;
            Errors.Add(message);
        }

        public void Dispose()
        {
            MatImage?.Dispose();
            MatImage = null;
            (CogImage as IDisposable)?.Dispose();
            CogImage = null;
        }
    }

    public interface IVisionStep
    {
        string Name { get; }

        /// <summary>
        /// true 이면 이 스텝이 실패해도 파이프라인을 계속 진행합니다.
        /// </summary>
        bool ContinueOnFailure { get; }

        void Execute(VisionContext context);
    }

    // ─────────────────────────────────────────────────────────────────────
    // 이미지 타입 호환성 시스템
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>파이프라인 스텝이 처리하는 이미지 채널 종류.</summary>
    public enum ImageType
    {
        /// <summary>제약 없음 — 어떤 이미지도 허용/출력.</summary>
        Any,
        /// <summary>그레이스케일 — CogImage8Grey / CV_8UC1.</summary>
        Grey,
        /// <summary>컬러 RGB — CogImage24PlanarColor / CV_8UC3.</summary>
        Color,
    }

    /// <summary>
    /// 스텝의 파라미터를 XML 요소로 저장/복원하는 선택적 인터페이스.
    /// PipelineManager가 이 인터페이스를 통해 각 스텝의 설정을 직렬화합니다.
    /// </summary>
    public interface IStepSerializable
    {
        void SaveParams(XElement el);
        void LoadParams(XElement el);
    }

    /// <summary>
    /// 검사 영역(CogRectangleAffine)을 가지는 스텝이 구현하는 인터페이스.
    /// VisionForm이 이 인터페이스를 통해 스텝별 Region을 독립적으로 설정합니다.
    /// </summary>
    public interface IRegionStep
    {
        CogRectangleAffine Region { get; set; }

        /// <summary>Region 없이 전체 이미지로 실행 가능하면 true.</summary>
        bool RegionRequired { get; }
    }

    /// <summary>
    /// 스텝의 입출력 이미지 타입을 선언하는 선택적 인터페이스.
    /// 파이프라인 편집기가 이 정보를 사용하여 스텝 간 호환성을 검사합니다.
    /// 이 인터페이스를 구현하지 않는 스텝은 Any→Any로 취급합니다.
    /// </summary>
    public interface IImageTypedStep
    {
        /// <summary>이 스텝이 필요로 하는 입력 이미지 타입.</summary>
        ImageType RequiredInputType  { get; }
        /// <summary>이 스텝이 생성하는 출력 이미지 타입.</summary>
        ImageType ProducedOutputType { get; }
    }
}
