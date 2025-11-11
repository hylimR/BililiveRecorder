using System.Linq;
using BililiveRecorder.Core.Api;
using BililiveRecorder.Core.Config.V3;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core.Config
{
    /// <summary>
    /// Helper to standardize room configurations to use RoomUrl format
    /// </summary>
    public static class ConfigStandardizationHelper
    {
        /// <summary>
        /// Standardize all room configs to use RoomUrl instead of RoomId
        /// This ensures consistent configuration format across all platforms
        /// </summary>
        public static void StandardizeRoomConfigs(ConfigV3 config, ILogger? logger = null)
        {
            var log = logger ?? Log.Logger;
            var standardized = 0;

            foreach (var room in config.Rooms)
            {
                // Skip if already using RoomUrl
                if (!string.IsNullOrEmpty(room.RoomUrl))
                    continue;

                // Skip if RoomId is not set
                if (room.RoomId == 0)
                    continue;

                // Determine platform
                var platform = room.Platform;

                // Convert RoomId to RoomUrl format
                switch (platform)
                {
                    case StreamingPlatform.Bilibili:
                        room.RoomUrl = $"https://live.bilibili.com/{room.RoomId}";
                        log.Information("Standardized Bilibili room {RoomId} to URL format", room.RoomId);
                        standardized++;
                        break;

                    case StreamingPlatform.Douyin:
                        room.RoomUrl = $"https://live.douyin.com/{room.RoomId}";
                        log.Information("Standardized Douyin room {RoomId} to URL format", room.RoomId);
                        standardized++;
                        break;

                    default:
                        log.Warning("Unknown platform {Platform} for room {RoomId}, assuming Bilibili", platform, room.RoomId);
                        room.RoomUrl = $"https://live.bilibili.com/{room.RoomId}";
                        standardized++;
                        break;
                }
            }

            if (standardized > 0)
            {
                log.Information("Standardized {Count} room configuration(s) to RoomUrl format", standardized);
            }
        }

        /// <summary>
        /// Check if config needs standardization
        /// </summary>
        public static bool NeedsStandardization(ConfigV3 config)
        {
            return config.Rooms.Any(r => r.RoomId != 0 && string.IsNullOrEmpty(r.RoomUrl));
        }
    }
}
