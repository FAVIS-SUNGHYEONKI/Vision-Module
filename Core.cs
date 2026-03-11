using System;
using System.Collections.Generic;
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
            (CogImage as IDisposable)?.Dispose();
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
}
