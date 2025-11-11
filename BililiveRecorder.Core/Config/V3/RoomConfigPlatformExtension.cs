using BililiveRecorder.Core.Api;
using Newtonsoft.Json;

#nullable enable
namespace BililiveRecorder.Core.Config.V3
{
    public partial class RoomConfig
    {
        private StreamingPlatform? _platform = null;
        private string _roomUrl = string.Empty;

        /// <summary>
        /// 直播平台 (Bilibili, Douyin)
        /// If not explicitly set, will be auto-detected from RoomUrl
        /// </summary>
        [JsonProperty("Platform")]
        public StreamingPlatform Platform
        {
            get
            {
                // Auto-detect platform from RoomUrl if not explicitly set
                if (_platform == null && !string.IsNullOrEmpty(_roomUrl))
                {
                    return PlatformDetector.DetectPlatform(_roomUrl);
                }
                return _platform ?? StreamingPlatform.Bilibili;
            }
            set => this._platform = value;
        }

        /// <summary>
        /// 房间URL或房间号
        /// Platform will be auto-detected from this URL if Platform is not explicitly set.
        ///
        /// Examples:
        /// - Bilibili: "123456", "https://live.bilibili.com/123456"
        /// - Douyin: "https://live.douyin.com/123456789", "https://v.douyin.com/xxx"
        /// </summary>
        [JsonProperty("RoomUrl")]
        public string RoomUrl
        {
            get => this._roomUrl;
            set
            {
                this._roomUrl = value ?? string.Empty;

                // Auto-detect and normalize if platform is not explicitly set
                if (_platform == null && !string.IsNullOrEmpty(_roomUrl))
                {
                    var detectedPlatform = PlatformDetector.DetectPlatform(_roomUrl);
                    this._roomUrl = PlatformDetector.NormalizeRoomIdentifier(_roomUrl, detectedPlatform);
                    // Store the detected platform so it doesn't get re-detected as Bilibili from numeric ID
                    this._platform = detectedPlatform;
                }
            }
        }

        /// <summary>
        /// Gets the normalized room identifier for API calls
        /// </summary>
        [JsonIgnore]
        public string NormalizedRoomIdentifier
        {
            get
            {
                if (!string.IsNullOrEmpty(_roomUrl))
                {
                    return PlatformDetector.NormalizeRoomIdentifier(_roomUrl, this.Platform);
                }
                return this.RoomId.ToString();
            }
        }

        /// <summary>
        /// Determines whether RoomUrl should be serialized to JSON
        /// </summary>
        public bool ShouldSerializeRoomUrl()
        {
            return !string.IsNullOrEmpty(_roomUrl);
        }
    }
}
