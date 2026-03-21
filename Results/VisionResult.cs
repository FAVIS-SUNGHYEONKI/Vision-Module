using System;
using System.Collections.Generic;
using System.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.Blob;
using Cognex.VisionPro.Caliper;
using Vision.Steps.VisionPro;

namespace Vision
{
    // ════════════════════════════════════════════════════════════════════
    // VisionResult  —  파이프라인 실행 결과의 타입화된 컨테이너
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 파이프라인 한 번 실행의 결과를 담는 불변 객체.
    /// (한번 만들어진 결과는 수정이 불가능하다.)
    ///
    /// 사용 예:
    /// <code>
    /// var result = await controller.RunAsync(image);
    /// if (result.IsSuccess)
    /// {
    ///     foreach (var edge in result.CaliperEdges)
    ///         Console.WriteLine($"X={edge.X:F1} Y={edge.Y:F1}");
    ///     foreach (var dist in result.Distances)
    ///         Console.WriteLine($"거리={dist.Distance:F2} px");
    /// }
    /// </code>
    /// </summary>
    public sealed class VisionResult
    {
        /// <summary>파이프라인이 오류 없이 완료되었으면 true.</summary>
        public bool IsSuccess { get; }

        //외부에서 Add/Remove를 하지 못하도록 IReadOnlyList<T>로 선언

        /// <summary>발생한 오류 메시지 목록. IsSuccess == true면 비어 있다.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// 모든 CogCaliperStep 결과를 평탄화한 에지 목록.
        /// CaliperId로 어느 Caliper 스텝의 결과인지 식별한다.
        /// </summary>
        public IReadOnlyList<CaliperEdge> CaliperEdges { get; }

        /// <summary>CogBlobStep에서 검출된 Blob 목록.</summary>
        public IReadOnlyList<BlobItem> Blobs { get; }

        /// <summary>CogCaliperDistanceStep에서 측정된 거리 목록.</summary>
        public IReadOnlyList<DistanceMeasurement> Distances { get; }

        private VisionResult(
            bool isSuccess,
            IReadOnlyList<string> errors,
            IReadOnlyList<CaliperEdge> caliperEdges,
            IReadOnlyList<BlobItem> blobs,
            IReadOnlyList<DistanceMeasurement> distances)
        {
            IsSuccess    = isSuccess;
            Errors       = errors;
            CaliperEdges = caliperEdges;
            Blobs        = blobs;
            Distances    = distances;
        }

        /// <summary>실패 결과를 나타내는 빈 인스턴스.</summary>
        public static VisionResult Empty { get; } =
            new VisionResult(false,
                new List<string> { "파이프라인이 구성되지 않았습니다." },
                new List<CaliperEdge>(),
                new List<BlobItem>(),
                new List<DistanceMeasurement>());

        /// <summary>특정 Caliper 스텝(caliperId)의 에지만 필터링한다.</summary>
        public IEnumerable<CaliperEdge> GetEdges(int caliperId)
            => CaliperEdges.Where(e => e.CaliperId == caliperId);

        /// <summary>특정 Blob 스텝(blobStepId)의 Blob만 필터링한다.</summary>
        public IEnumerable<BlobItem> GetBlobs(int blobStepId)
            => Blobs.Where(b => b.BlobStepId == blobStepId);

        // ── 팩토리: VisionContext → VisionResult ─────────────────────

        /// <summary>
        /// VisionContext를 타입화된 VisionResult로 변환한다.
        /// PipelineController.RunAsync() 내부에서 호출된다.
        /// </summary>
        internal static VisionResult FromContext(
            VisionContext context,
            IReadOnlyList<IVisionStep> steps)
        {
            var caliperEdges = new List<CaliperEdge>();
            var blobs        = new List<BlobItem>();
            var distances    = new List<DistanceMeasurement>();

            // ── Caliper 에지 수집 ──────────────────────────────────────
            var caliperSteps = steps.OfType<CogCaliperStep>().ToList();
            int cIdx = 0;
            foreach (var key in context.Data.Keys
                .Where(k => k.StartsWith("VisionPro.Caliper.")
                         && !k.EndsWith(".Region"))
                .OrderBy(k => k))
            {
                var results = context.Data[key] as CogCaliperResults;
                if (results == null) { cIdx++; continue; }

                var region = cIdx < caliperSteps.Count
                    ? caliperSteps[cIdx].Region : null;

                for (int i = 0; i < results.Count; i++)
                {
                    var r = results[i];
                    double x, y;
                    if (region != null)
                    {
                        x = region.CenterX + Math.Cos(region.Rotation) * r.Position;
                        y = region.CenterY + Math.Sin(region.Rotation) * r.Position;
                    }
                    else { x = r.Position; y = 0; }

                    caliperEdges.Add(new CaliperEdge(cIdx, i, x, y, r.Score, r.Position));
                }
                cIdx++;
            }

            // ── Blob 수집 ─────────────────────────────────────────────
            int bIdx = 0;
            foreach (var key in context.Data.Keys
                .Where(k => k.StartsWith("VisionPro.Blob."))
                .OrderBy(k => k))
            {
                var blobResults = context.Data[key] as CogBlobResults;
                if (blobResults != null)
                {
                    int itemIdx = 0;
                    foreach (CogBlobResult b in blobResults.GetBlobs())
                        blobs.Add(new BlobItem(bIdx, itemIdx++, b));
                }
                bIdx++;
            }

            // ── 거리 측정 수집 ────────────────────────────────────────
            foreach (var key in context.Data.Keys
                .Where(k => k.StartsWith("VisionPro.CaliperDistance."))
                .OrderBy(k => k))
            {
                var d = context.Data[key] as CaliperDistanceResult;
                if (d != null)
                    distances.Add(new DistanceMeasurement(d));
            }

            return new VisionResult(
                context.IsSuccess,
                context.Errors.AsReadOnly(),
                caliperEdges,
                blobs,
                distances);
        }
    }

    // ════════════════════════════════════════════════════════════════════
    // CaliperEdge  —  Caliper 에지 하나의 2D 이미지 좌표 + 점수
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CogCaliperStep 하나의 에지 검출 결과.
    /// </summary>
    public sealed class CaliperEdge
    {
        /// <summary>이 에지를 생성한 CogCaliperStep의 파이프라인 순서 인덱스 (0-based).</summary>
        public int CaliperId { get; }

        /// <summary>해당 Caliper 결과 내 에지 인덱스 (0-based).</summary>
        public int EdgeIndex { get; }

        /// <summary>이미지 X 좌표 (pixels).</summary>
        public double X { get; }

        /// <summary>이미지 Y 좌표 (pixels).</summary>
        public double Y { get; }

        /// <summary>에지 신뢰도 점수 (0~1).</summary>
        public double Score { get; }

        /// <summary>Caliper 스캔 축의 1D 원시 위치.</summary>
        public double Position { get; }

        internal CaliperEdge(int caliperId, int edgeIndex,
            double x, double y, double score, double position)
        {
            CaliperId  = caliperId;
            EdgeIndex  = edgeIndex;
            X          = x;
            Y          = y;
            Score      = score;
            Position   = position;
        }

        public override string ToString()
            => $"Caliper[{CaliperId}] Edge[{EdgeIndex}]"
             + $" X={X:F1} Y={Y:F1} Score={Score:F3}";
    }

    // ════════════════════════════════════════════════════════════════════
    // BlobItem  —  Blob 하나의 검출 결과
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CogBlobStep에서 검출된 Blob 하나의 결과.
    /// </summary>
    public sealed class BlobItem
    {
        /// <summary>이 Blob을 생성한 CogBlobStep의 파이프라인 순서 인덱스 (0-based).</summary>
        public int BlobStepId { get; }

        /// <summary>해당 Blob 스텝 결과 내 Blob 인덱스 (0-based).</summary>
        public int BlobIndex { get; }

        /// <summary>면적 (pixels²).</summary>
        public double Area { get; }

        /// <summary>무게 중심 X 좌표 (pixels).</summary>
        public double CenterX { get; }

        /// <summary>무게 중심 Y 좌표 (pixels).</summary>
        public double CenterY { get; }

        /// <summary>Display에 외곽선 그래픽을 그릴 때 사용하는 Cognex 원시 객체.</summary>
        internal CogBlobResult RawResult { get; }

        internal BlobItem(int blobStepId, int blobIndex, CogBlobResult raw)
        {
            BlobStepId = blobStepId;
            BlobIndex  = blobIndex;
            RawResult  = raw;
            Area       = raw.Area;
            CenterX    = raw.CenterOfMassX;
            CenterY    = raw.CenterOfMassY;
        }

        public override string ToString()
            => $"Blob[{BlobStepId}][{BlobIndex}] Area={Area:F0} Center=({CenterX:F1},{CenterY:F1})";
    }

    // ════════════════════════════════════════════════════════════════════
    // DistanceMeasurement  —  두 Caliper 에지 간 거리 측정 결과
    // ════════════════════════════════════════════════════════════════════

    /// <summary>
    /// CogCaliperDistanceStep에서 측정된 두 점 간의 거리.
    /// </summary>
    public sealed class DistanceMeasurement
    {
        /// <summary>측정에 사용한 Caliper A 인덱스.</summary>
        public int CaliperId_A { get; }

        /// <summary>측정에 사용한 Caliper B 인덱스.</summary>
        public int CaliperId_B { get; }

        /// <summary>점 A의 이미지 X 좌표 (pixels).</summary>
        public double X1 { get; }

        /// <summary>점 A의 이미지 Y 좌표 (pixels).</summary>
        public double Y1 { get; }

        /// <summary>점 B의 이미지 X 좌표 (pixels).</summary>
        public double X2 { get; }

        /// <summary>점 B의 이미지 Y 좌표 (pixels).</summary>
        public double Y2 { get; }

        /// <summary>유클리드 거리 (pixels). √((X2-X1)²+(Y2-Y1)²)</summary>
        public double Distance { get; }

        internal DistanceMeasurement(CaliperDistanceResult raw)
        {
            CaliperId_A = raw.CaliperId_A;
            CaliperId_B = raw.CaliperId_B;
            X1          = raw.X1;
            Y1          = raw.Y1;
            X2          = raw.X2;
            Y2          = raw.Y2;
            Distance    = raw.Distance;
        }

        public override string ToString()
            => $"Distance[{CaliperId_A}→{CaliperId_B}]"
             + $" A=({X1:F1},{Y1:F1}) B=({X2:F1},{Y2:F1})"
             + $" dist={Distance:F2} px";
    }
}
