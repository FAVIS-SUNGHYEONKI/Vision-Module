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
    /// 혼합 파이프라인에서 CvStepBase / VpStepBase가 내부적으로 사용한다.
    /// 직접 호출도 가능하다.
    /// </summary>
    public static class ImageConverter
    {
        // ─────────────────────────────────────────────
        // Mat → CogImage
        // ─────────────────────────────────────────────

        /// <summary>
        /// OpenCV Mat(그레이 또는 컬러) → CogImage8Grey.
        /// 컬러 이미지는 그레이스케일로 변환 후 복사한다.
        /// </summary>
        public static CogImage8Grey ToCogImage8Grey(Mat mat)
        {
            if (mat == null || mat.Empty())
                throw new ArgumentNullException(nameof(mat));

            // 컬러 Mat은 그레이로 변환. 이미 그레이이면 그대로 사용한다.
            bool converted = mat.Channels() > 1;
            Mat gray = converted ? mat.CvtColor(ColorConversionCodes.BGR2GRAY) : mat;
           
            try
            {
                
                var cogImg = new CogImage8Grey();
                // Mat 이미지의 가로x세로 크기의 내부 픽셀 버퍼를 할당한다.
                // 이때 VisionPro는 내부적으로 메모리를 할당하여 관리한다.
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
                    pixMem?.Dispose();
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
        /// CogImage8Grey 이외의 포맷은 VisionPro 내부 변환을 거친다.
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
        // 내부 헬퍼
        // ─────────────────────────────────────────────

        /// <summary>
        /// CogImage8Grey → OpenCV Mat (CV_8UC1) 내부 변환.
        /// 픽셀 메모리(ICogImage8PixelMemory)를 통해 행 단위로 복사한다.
        /// Marshal.Copy를 사용하여 관리/비관리 메모리 경계를 넘어 데이터를 복사한다.
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
                pixMem?.Dispose();
            }

            return mat;
        }
    }
}
