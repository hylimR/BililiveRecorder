using System;
using System.Linq;
using System.Threading.Tasks;
using BililiveRecorder.Core.Config.V3;

namespace BililiveRecorder.Core.Api.Http
{
    internal class BilibiliApiClient : IPlatformApiClient
    {
        private readonly HttpApiClient httpApiClient;
        private bool disposedValue;

        public StreamingPlatform Platform => StreamingPlatform.Bilibili;

        public BilibiliApiClient(GlobalConfig config)
        {
            this.httpApiClient = new HttpApiClient(config);
        }

        public async Task<PlatformRoomInfo> GetRoomInfoAsync(string roomIdentifier)
        {
            if (!int.TryParse(roomIdentifier, out var roomId))
            {
                throw new ArgumentException($"Invalid Bilibili room ID: {roomIdentifier}");
            }

            var response = await this.httpApiClient.GetRoomInfoAsync(roomId);

            if (response.Data == null)
            {
                throw new Exception("Failed to get room info");
            }

            var data = response.Data;

            return new PlatformRoomInfo
            {
                RoomId = data.Room.RoomId.ToString(),
                AnchorName = data.User.BaseInfo.Name ?? string.Empty,
                Title = data.Room.Title ?? string.Empty,
                IsLive = data.Room.LiveStatus == 1,
                Uid = data.Room.Uid,
                AreaParent = data.Room.ParentAreaName ?? string.Empty,
                AreaChild = data.Room.AreaName ?? string.Empty,
                CoverUrl = string.Empty
            };
        }

        public async Task<PlatformStreamInfo> GetStreamUrlAsync(string roomIdentifier, int qualityNumber)
        {
            if (!int.TryParse(roomIdentifier, out var roomId))
            {
                throw new ArgumentException($"Invalid Bilibili room ID: {roomIdentifier}");
            }

            var response = await this.httpApiClient.GetStreamUrlAsync(roomId, qualityNumber);

            if (response.Data == null || response.Data.PlayurlInfo == null)
            {
                throw new Exception("Failed to get stream URL");
            }

            var playurlInfo = response.Data.PlayurlInfo;
            var stream = playurlInfo?.Playurl?.Streams?.FirstOrDefault();

            if (stream == null)
            {
                throw new Exception("No stream available");
            }

            // Try to get the best quality format
            var format = stream.Formats?.FirstOrDefault(f => f.FormatName == "flv") ?? stream.Formats?.FirstOrDefault();

            if (format == null || format.Codecs == null || format.Codecs.Length == 0)
            {
                throw new Exception("No valid format available");
            }

            var codec = format.Codecs.FirstOrDefault();

            if (codec?.UrlInfos == null || codec.UrlInfos.Length == 0)
            {
                throw new Exception("No stream URL available");
            }

            var urlInfo = codec.UrlInfos.FirstOrDefault();

            if (string.IsNullOrEmpty(urlInfo?.Host) || string.IsNullOrEmpty(urlInfo.Extra))
            {
                throw new Exception("Invalid stream URL");
            }

            var streamUrl = urlInfo.Host + codec.BaseUrl + urlInfo.Extra;

            return new PlatformStreamInfo
            {
                StreamUrl = streamUrl,
                Quality = codec.CurrentQn.ToString(),
                Format = StreamFormat.FLV,
                Codec = codec.CodecName ?? "avc",
                QualityNumber = codec.CurrentQn
            };
        }

        public void Dispose()
        {
            if (!this.disposedValue)
            {
                this.httpApiClient?.Dispose();
                this.disposedValue = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
