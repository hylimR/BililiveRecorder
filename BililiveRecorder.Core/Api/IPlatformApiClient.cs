using System;
using System.Threading.Tasks;

namespace BililiveRecorder.Core.Api
{
    public enum StreamingPlatform
    {
        Bilibili,
        Douyin
    }

    public enum StreamFormat
    {
        FLV,
        HLS
    }

    public class PlatformRoomInfo
    {
        public string RoomId { get; set; } = string.Empty;
        public string AnchorName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public bool IsLive { get; set; }
        public long Uid { get; set; }
        public string AreaParent { get; set; } = string.Empty;
        public string AreaChild { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
    }

    public class PlatformStreamInfo
    {
        public string StreamUrl { get; set; } = string.Empty;
        public string Quality { get; set; } = string.Empty;
        public StreamFormat Format { get; set; }
        public string Codec { get; set; } = string.Empty;
        public int QualityNumber { get; set; }
    }

    internal interface IPlatformApiClient : IDisposable
    {
        StreamingPlatform Platform { get; }

        Task<PlatformRoomInfo> GetRoomInfoAsync(string roomIdentifier);
        Task<PlatformStreamInfo> GetStreamUrlAsync(string roomIdentifier, int qualityNumber);
    }
}
