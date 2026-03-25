using System.Collections.Generic;
using Cognex.VisionPro;

namespace Vision
{
    /// <summary>
    /// 단일 스텝 실행의 모든 산출물을 담는 불변 결과 객체.
    ///
    /// 사용 예:
    /// <code>
    /// _controller.SetInputImage(image);
    /// var step   = _controller.Steps[2];
    /// var result = _controller.RunStep(step);
    ///
    /// if (result.IsSuccess)
    /// {
    ///     cogDisplay.Image = result.InputImage;
    ///     foreach (var edge in result.CaliperEdges)
    ///         Console.WriteLine($"X={edge.X:F1} Y={edge.Y:F1}");
    /// }
    /// </code>
    /// </summary>
    public sealed class StepResult
    {
        /// <summary>스텝이 오류 없이 완료되었으면 true.</summary>
        public bool IsSuccess { get; internal set; }

        /// <summary>실패 시 오류 메시지. 성공이면 null.</summary>
        public string Error { get; internal set; }

        /// <summary>스텝이 처리한 입력 이미지.</summary>
        public ICogImage InputImage { get; internal set; }

        /// <summary>
        /// 처리 스텝(WeightedRGB, ConvertGrey, Threshold 등)이 생산한 출력 이미지.
        /// 검사 스텝(Caliper, Blob 등)이면 null.
        /// </summary>
        public ICogImage OutputImage { get; internal set; }

        /// <summary>CogCaliperStep 결과 에지 목록. Caliper 스텝이 아니면 비어 있다.</summary>
        public IReadOnlyList<CaliperEdge> CaliperEdges { get; internal set; }

        /// <summary>CogBlobStep 검출 결과 목록. Blob 스텝이 아니면 비어 있다.</summary>
        public IReadOnlyList<BlobItem> Blobs { get; internal set; }

        /// <summary>CogCaliperDistanceStep 거리 측정 목록. 거리 스텝이 아니면 비어 있다.</summary>
        public IReadOnlyList<DistanceMeasurement> Distances { get; internal set; }

        internal static readonly IReadOnlyList<CaliperEdge>       EmptyEdges     = new List<CaliperEdge>();
        internal static readonly IReadOnlyList<BlobItem>          EmptyBlobs     = new List<BlobItem>();
        internal static readonly IReadOnlyList<DistanceMeasurement> EmptyDists   = new List<DistanceMeasurement>();

        /// <summary>실패 결과를 빠르게 생성한다.</summary>
        internal static StepResult Failure(string error) => new StepResult
        {
            IsSuccess    = false,
            Error        = error,
            CaliperEdges = EmptyEdges,
            Blobs        = EmptyBlobs,
            Distances    = EmptyDists,
        };
    }
}
