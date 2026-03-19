using System;

namespace Vision.Steps.VisionPro
{
    /// <summary>
    /// CogCaliperDistanceStep의 실행 결과를 담는 데이터 클래스.
    ///
    /// VisionContext.Data["VisionPro.CaliperDistance.{N}"] 키로 저장됩니다.
    /// 두 Caliper 에지 점의 2D 이미지 좌표와 유클리드 거리를 포함합니다.
    /// </summary>
    public class CaliperDistanceResult
    {
        /// <summary>Caliper A 에지의 이미지 X 좌표 (pixels).</summary>
        public double X1 { get; set; }

        /// <summary>Caliper A 에지의 이미지 Y 좌표 (pixels).</summary>
        public double Y1 { get; set; }

        /// <summary>Caliper B 에지의 이미지 X 좌표 (pixels).</summary>
        public double X2 { get; set; }

        /// <summary>Caliper B 에지의 이미지 Y 좌표 (pixels).</summary>
        public double Y2 { get; set; }

        /// <summary>
        /// 두 점 사이의 유클리드 거리 (pixels).
        /// Distance = √((X2-X1)² + (Y2-Y1)²)
        /// </summary>
        public double Distance =>
            Math.Sqrt((X2 - X1) * (X2 - X1) + (Y2 - Y1) * (Y2 - Y1));

        /// <summary>사용한 Caliper A 결과 인덱스 (0-based).</summary>
        public int CaliperId_A { get; set; }

        /// <summary>Caliper A 결과 내 에지 인덱스 (0-based).</summary>
        public int EdgeIndex_A { get; set; }

        /// <summary>사용한 Caliper B 결과 인덱스 (0-based).</summary>
        public int CaliperId_B { get; set; }

        /// <summary>Caliper B 결과 내 에지 인덱스 (0-based).</summary>
        public int EdgeIndex_B { get; set; }
    }
}
