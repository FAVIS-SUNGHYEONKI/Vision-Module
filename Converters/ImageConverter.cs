using System;
using System.Runtime.InteropServices;
using Cognex.VisionPro;
using OpenCvSharp;
using OpenCvSharp.Extensions;

namespace Vision.Converters
{
    /// <summary>
    /// OpenCV Mat ↔ Cognex ICogImage 변환 유틸리티.
    ///
    /// 혼합 파이프라인에서 CvStepBase / VpStepBase가 내부적으로 사용합니다.
    /// 직접 호출도 가능합니다.
    /// </summary>
    public static class ImageConverter
    {
        // ─────────────────────────────────────────────
        // Mat → CogImage
        // ─────────────────────────────────────────────

        /// <summary>
        /// OpenCV Mat(8UC1 또는 컬러) → CogImage8Grey.
        /// 컬러 이미지는 그레이스케일로 변환 후 복사합니다.
        /// </summary>
        public static CogImage8Grey ToCogImage8Grey(Mat mat)
        {
            if (mat == null || mat.Empty())
                throw new ArgumentNullException(nameof(mat));

            bool converted = mat.Channels() > 1;
            Mat gray = converted ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat;

            try
            {
                var cogImg = new CogImage8Grey();
                cogImg.Allocate(gray.Width, gray.Height);
                
                ICogImage8PixelMemory pixMem = null;
                try
                {
                    pixMem = cogImg.Get8GreyPixelMemory(
                        CogImageDataModeConstants.ReadWrite, 0, 0, gray.Width, gray.Height);

                    byte[] row = new byte[gray.Width];
                    for (int y = 0; y < gray.Height; y++)
                    {
                        Marshal.Copy(gray.Ptr(y), row, 0, gray.Width);
                        Marshal.Copy(row, 0, pixMem.Scan0 + y * pixMem.Stride, gray.Width);
                    }
                }
                finally
                {
                    if (pixMem != null)
                        cogImg.Dispose();
                }

                return cogImg;
            }
            finally
            {
                if (converted) gray.Dispose();
            }
        }

        // ─────────────────────────────────────────────
        // ICogImage → Mat
        // ─────────────────────────────────────────────

        /// <summary>
        /// ICogImage → OpenCV Mat (CV_8UC1).
        /// CogImage8Grey 이외의 포맷은 VisionPro 내부 변환을 거칩니다.
        /// </summary>
        public static Mat ToMat(ICogImage cogImage)
        {
            if (cogImage == null)
                throw new ArgumentNullException(nameof(cogImage));

            // 이미 8비트 그레이이면 바로 복사
            if (cogImage is CogImage8Grey grey8)
                return ToMat(grey8);

            // 다른 포맷은 8Grey로 변환 후 처리
            var converted = CogImageConvert.GetIntensityImage(cogImage, 0, 0, cogImage.Width, cogImage.Height);

            if (converted == null)
                throw new NotSupportedException(
                    $"ICogImage 타입 '{cogImage.GetType().Name}'은 지원하지 않습니다.");

            using (converted)
                return ToMat(converted);
        }

        // ─────────────────────────────────────────────
        // ICogImage → 컬러 Mat (BGR 3채널)
        // ─────────────────────────────────────────────

        /// <summary>
        /// ICogImage → OpenCV Mat.
        /// - CogImage8Grey          : CV_8UC1 (1채널 그레이)
        /// - CogImage24PlanarColor  : CV_8UC3 (BGR 3채널)
        /// - 기타                   : 강도 변환(그레이스케일) 후 CV_8UC1
        ///
        /// ※ CogImage24PlanarColor 픽셀 메모리 레이아웃 가정
        ///   Scan0 → [R plane][G plane][B plane] (각 plane = Height × RowStride bytes)
        ///   실제 VisionPro 버전에 따라 Scan0 / RowStride 프로퍼티 이름이 다를 수 있습니다.
        /// </summary>
        public static Mat ToColorMat(ICogImage cogImage)
        {
            if (cogImage == null)
                throw new ArgumentNullException(nameof(cogImage));

            if (cogImage is CogImage8Grey)
                return ToMat(cogImage);  // 이미 그레이

            //if (cogImage is CogImage24PlanarColor colorImg)
            //    return ExtractColorMat(colorImg);

            return ToMat(cogImage);  // 그 외: 그레이스케일 fallback
        }

        // ─────────────────────────────────────────────
        // 내부 헬퍼
        // ─────────────────────────────────────────────

        /// <summary>
        /// CogImage8Grey → OpenCV Mat (CV_8UC1) 내부 변환.
        /// 픽셀 메모리(ICogImage8PixelMemory)를 통해 행 단위로 복사합니다.
        /// Marshal.Copy를 사용하여 관리/비관리 메모리 경계를 넘어 데이터를 복사합니다.
        /// </summary>
        private static Mat ToMat(CogImage8Grey cogImg)
        {
            var mat = new Mat(cogImg.Height, cogImg.Width, MatType.CV_8UC1);

            ICogImage8PixelMemory pixMem = null;

            try
            {
                pixMem = cogImg.Get8GreyPixelMemory(
                    CogImageDataModeConstants.Read, 0, 0, cogImg.Width, cogImg.Height);

                byte[] row = new byte[cogImg.Width];
                for (int y = 0; y < cogImg.Height; y++)
                {
                    Marshal.Copy(pixMem.Scan0 + y * pixMem.Stride, row, 0, cogImg.Width);
                    Marshal.Copy(row, 0, mat.Ptr(y), cogImg.Width);
                }
            }
            finally
            {
                if (pixMem != null)
                    cogImg.Dispose();
            }

            return mat;
        }
    }
}
