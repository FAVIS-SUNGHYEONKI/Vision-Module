using System;
using Cognex.VisionPro;
using Cognex.VisionPro.Caliper;
using Cognex.VisionPro.Blob;
using Vision.Steps.VisionPro;

namespace Vision.UI
{
    /// <summary>
    /// CogDisplay에 Vision 결과 그래픽을 그리는 정적 헬퍼 클래스.
    ///
    /// PipelineController.DrawResults()와 PipelineEditorForm 내부에서 공통으로 사용된다.
    /// </summary>
    public static class DisplayHelper
    {
        // ── 전체 VisionResult 한 번에 그리기 ────────────────────────────

        /// <summary>
        /// VisionResult의 모든 결과 그래픽(에지, Blob, 거리선)을
        /// CogDisplay의 StaticGraphics에 추가한다.
        /// display.StaticGraphics.Clear()는 호출자가 책임진다.
        /// </summary>
        public static void DrawAllResults(
            Cognex.VisionPro.Display.CogDisplay display,
            VisionResult result)
        {
            if (display == null || result == null) return;

            // Caliper 에지
            foreach (var edge in result.CaliperEdges)
                DrawEdge(display, edge);

            // Blob 외곽선
            foreach (var blob in result.Blobs)
                DrawBlob(display, blob);

            // 거리 측정선
            for (int i = 0; i < result.Distances.Count; i++)
                DrawDistance(display, result.Distances[i], i);
        }

        // ── 개별 그리기 메서드 ───────────────────────────────────────────

        /// <summary>Caliper 에지 위치에 초록 십자 마커를 그린다.</summary>
        public static void DrawEdge(
            Cognex.VisionPro.Display.CogDisplay display,
            CaliperEdge edge)
        {
            try
            {
                string prefix = "edge_" + edge.CaliperId + "_" + edge.EdgeIndex;

                var mh = new CogLineSegment();
                mh.SetStartEnd(edge.X - 10, edge.Y, edge.X + 10, edge.Y);
                mh.Color = CogColorConstants.Green;
                display.StaticGraphics.Add(mh, prefix + "_h");

                var mv = new CogLineSegment();
                mv.SetStartEnd(edge.X, edge.Y - 10, edge.X, edge.Y + 10);
                mv.Color = CogColorConstants.Green;
                display.StaticGraphics.Add(mv, prefix + "_v");
            }
            catch { }
        }

        /// <summary>Blob 외곽선(GetBoundary 폴리곤)을 노란색으로 그린다.</summary>
        public static void DrawBlob(
            Cognex.VisionPro.Display.CogDisplay display,
            BlobItem blob)
        {
            try
            {
                var boundary = blob.RawResult?.GetBoundary();
                if (boundary != null)
                {
                    boundary.Color = CogColorConstants.Yellow;
                    display.StaticGraphics.Add(
                        boundary,
                        "blob_" + blob.BlobStepId + "_" + blob.BlobIndex + "_boundary");
                }
            }
            catch { }
        }

        /// <summary>두 점을 잇는 마젠타 거리 측정선과 끝점 마커를 그린다.</summary>
        public static void DrawDistance(
            Cognex.VisionPro.Display.CogDisplay display,
            DistanceMeasurement dist, int index)
        {
            try
            {
                var line = new CogLineSegment();
                line.SetStartEnd(dist.X1, dist.Y1, dist.X2, dist.Y2);
                line.Color = CogColorConstants.Magenta;
                display.StaticGraphics.Add(line, "dist_" + index + "_line");

                DrawCross(display, dist.X1, dist.Y1, 6, CogColorConstants.Magenta, "dist_" + index + "_a");
                DrawCross(display, dist.X2, dist.Y2, 6, CogColorConstants.Magenta, "dist_" + index + "_b");
            }
            catch { }
        }

        // ── 하위 호환: VisionContext 원시 타입 기반 메서드 ───────────────
        // (PipelineEditorForm에서 내부적으로 사용)

        internal static string CaliperResultToXY(
            CogCaliperResult r, CogRectangleAffine region)
        {
            if (region == null)
                return "Pos=" + r.Position.ToString("F2");
            double cos  = Math.Cos(region.Rotation);
            double sin  = Math.Sin(region.Rotation);
            double imgX = region.CenterX + cos * r.Position;
            double imgY = region.CenterY + sin * r.Position;
            return "X=" + imgX.ToString("F1") + " Y=" + imgY.ToString("F1");
        }

        internal static void DrawEdgeMarkerOnDisplay(
            Cognex.VisionPro.Display.CogDisplay display,
            CogCaliperResult result, int index,
            CogRectangleAffine region, string prefix)
        {
            if (region == null) return;
            try
            {
                double cos  = Math.Cos(region.Rotation);
                double sin  = Math.Sin(region.Rotation);
                double imgX = region.CenterX + cos * result.Position;
                double imgY = region.CenterY + sin * result.Position;

                var mh = new CogLineSegment();
                mh.SetStartEnd(imgX - 10, imgY, imgX + 10, imgY);
                mh.Color = CogColorConstants.Green;
                display.StaticGraphics.Add(mh, prefix + index + "_h");

                var mv = new CogLineSegment();
                mv.SetStartEnd(imgX, imgY - 10, imgX, imgY + 10);
                mv.Color = CogColorConstants.Green;
                display.StaticGraphics.Add(mv, prefix + index + "_v");
            }
            catch { }
        }

        internal static void DrawBlobMarkerOnDisplay(
            Cognex.VisionPro.Display.CogDisplay display,
            CogBlobResult b, int index)
        {
            try
            {
                var boundary = b.GetBoundary();
                if (boundary != null)
                {
                    boundary.Color = CogColorConstants.Yellow;
                    display.StaticGraphics.Add(boundary, "blob_" + index + "_boundary");
                }
            }
            catch { }
        }

        internal static void DrawDistanceLineOnDisplay(
            Cognex.VisionPro.Display.CogDisplay display,
            CaliperDistanceResult dist, int index)
        {
            try
            {
                var line = new CogLineSegment();
                line.SetStartEnd(dist.X1, dist.Y1, dist.X2, dist.Y2);
                line.Color = CogColorConstants.Magenta;
                display.StaticGraphics.Add(line, "dist_" + index + "_line");

                DrawCross(display, dist.X1, dist.Y1, 6, CogColorConstants.Magenta, "dist_" + index + "_a");
                DrawCross(display, dist.X2, dist.Y2, 6, CogColorConstants.Magenta, "dist_" + index + "_b");
            }
            catch { }
        }

        // ── 내부 유틸 ────────────────────────────────────────────────────

        private static void DrawCross(
            Cognex.VisionPro.Display.CogDisplay display,
            double x, double y, int size,
            CogColorConstants color, string keyPrefix)
        {
            var mh = new CogLineSegment();
            mh.SetStartEnd(x - size, y, x + size, y);
            mh.Color = color;
            display.StaticGraphics.Add(mh, keyPrefix + "_h");

            var mv = new CogLineSegment();
            mv.SetStartEnd(x, y - size, x, y + size);
            mv.Color = color;
            display.StaticGraphics.Add(mv, keyPrefix + "_v");
        }
    }
}
