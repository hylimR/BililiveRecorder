using System;
using System.Text.RegularExpressions;

namespace BililiveRecorder.Core.Api
{
    public static class PlatformDetector
    {
        private static readonly Regex DouyinUrlRegex = new Regex(
            @"(live\.douyin\.com|v\.douyin\.com|douyin\.com/user)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        private static readonly Regex BilibiliUrlRegex = new Regex(
            @"(live\.bilibili\.com|b23\.tv|bilibili\.com)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled
        );

        /// <summary>
        /// Detects the streaming platform from a given URL or room identifier
        /// </summary>
        /// <param name="urlOrRoomId">URL or room ID to detect platform from</param>
        /// <returns>Detected streaming platform, defaults to Bilibili if uncertain</returns>
        public static StreamingPlatform DetectPlatform(string urlOrRoomId)
        {
            if (string.IsNullOrWhiteSpace(urlOrRoomId))
            {
                return StreamingPlatform.Bilibili;
            }

            // Check for Douyin patterns
            if (DouyinUrlRegex.IsMatch(urlOrRoomId))
            {
                return StreamingPlatform.Douyin;
            }

            // Check for Bilibili patterns
            if (BilibiliUrlRegex.IsMatch(urlOrRoomId))
            {
                return StreamingPlatform.Bilibili;
            }

            // If it's a pure numeric string, assume Bilibili for backward compatibility
            if (long.TryParse(urlOrRoomId, out _))
            {
                return StreamingPlatform.Bilibili;
            }

            // Default to Bilibili for unknown patterns
            return StreamingPlatform.Bilibili;
        }

        /// <summary>
        /// Normalizes a URL or room identifier for the given platform
        /// </summary>
        /// <param name="urlOrRoomId">URL or room ID to normalize</param>
        /// <param name="platform">Target platform</param>
        /// <returns>Normalized identifier suitable for the platform API</returns>
        public static string NormalizeRoomIdentifier(string urlOrRoomId, StreamingPlatform platform)
        {
            if (string.IsNullOrWhiteSpace(urlOrRoomId))
            {
                return string.Empty;
            }

            switch (platform)
            {
                case StreamingPlatform.Bilibili:
                    return NormalizeBilibiliRoomId(urlOrRoomId);

                case StreamingPlatform.Douyin:
                    return NormalizeDouyinRoomId(urlOrRoomId);

                default:
                    return urlOrRoomId;
            }
        }

        private static string NormalizeBilibiliRoomId(string urlOrRoomId)
        {
            // Extract room ID from Bilibili URLs
            // Examples:
            // https://live.bilibili.com/123456 -> 123456
            // https://b23.tv/xxx -> keep as is (will be resolved by API)
            // 123456 -> 123456

            if (long.TryParse(urlOrRoomId, out _))
            {
                return urlOrRoomId;
            }

            // Try to extract room ID from URL
            var match = Regex.Match(urlOrRoomId, @"live\.bilibili\.com/(\d+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            // Return as-is for short URLs and other formats
            return urlOrRoomId;
        }

        private static string NormalizeDouyinRoomId(string urlOrRoomId)
        {
            // For Douyin, we extract just the room ID from URLs
            // The DouyinApiClient will construct the full URL
            // Examples:
            // https://live.douyin.com/123456789 -> 123456789
            // https://v.douyin.com/xxx -> https://v.douyin.com/xxx (short URL, keep as-is)
            // 123456789 -> 123456789 (direct room ID)

            // If it's a live.douyin.com URL, extract the room ID
            if (urlOrRoomId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                urlOrRoomId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Extract room ID from live.douyin.com URLs
                var match = Regex.Match(urlOrRoomId, @"live\.douyin\.com/(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                // Keep short URLs and user URLs as-is (they need to be resolved)
                if (urlOrRoomId.Contains("v.douyin.com") || urlOrRoomId.Contains("douyin.com/user/"))
                {
                    return urlOrRoomId;
                }
            }

            // If it's a pure numeric ID, return as-is
            if (long.TryParse(urlOrRoomId, out _))
            {
                return urlOrRoomId;
            }

            // If it looks like a short format without protocol, add https://
            if (urlOrRoomId.StartsWith("v.douyin.com", StringComparison.OrdinalIgnoreCase) ||
                urlOrRoomId.StartsWith("live.douyin.com", StringComparison.OrdinalIgnoreCase))
            {
                // Extract ID from live.douyin.com format
                var match = Regex.Match(urlOrRoomId, @"live\.douyin\.com/(\d+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                return "https://" + urlOrRoomId;
            }

            // Return as-is for unknown formats
            return urlOrRoomId;
        }

        /// <summary>
        /// Checks if the given string is a valid platform identifier
        /// </summary>
        public static bool IsValidRoomIdentifier(string urlOrRoomId)
        {
            if (string.IsNullOrWhiteSpace(urlOrRoomId))
            {
                return false;
            }

            // Valid if it's a URL
            if (urlOrRoomId.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                urlOrRoomId.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Valid if it's a numeric room ID
            if (long.TryParse(urlOrRoomId, out _))
            {
                return true;
            }

            // Valid if it matches known domain patterns
            return DouyinUrlRegex.IsMatch(urlOrRoomId) || BilibiliUrlRegex.IsMatch(urlOrRoomId);
        }
    }
}
