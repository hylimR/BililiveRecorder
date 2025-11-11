using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BililiveRecorder.Core.Config.V3;
using Newtonsoft.Json.Linq;
using Serilog;

namespace BililiveRecorder.Core.Api.Http
{
    internal class DouyinApiClient : IPlatformApiClient
    {
        private const string HttpHeaderUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private const string HttpHeaderReferer = "https://live.douyin.com/";

        private readonly HttpClient httpClient;
        private static readonly ILogger logger = Log.ForContext<DouyinApiClient>();
        private bool disposedValue;

        public StreamingPlatform Platform => StreamingPlatform.Douyin;

        // Quality mapping from Douyin to numeric values
        private static readonly Dictionary<string, int> QualityMapping = new Dictionary<string, int>
        {
            { "ORIGIN", 10000 },
            { "OD", 10000 },
            { "BD", 400 },
            { "UHD", 250 },
            { "HD", 150 },
            { "SD", 80 },
            { "LD", 80 }
        };

        public DouyinApiClient(GlobalConfig config)
        {
            // Use 2x timeout for Douyin API due to potential third-party API slowness
            var douyinTimeout = Math.Max(config.TimingApiTimeout * 2, 20000);

            this.httpClient = new HttpClient(new HttpClientHandler
            {
                UseCookies = false,
                UseDefaultCredentials = false,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            })
            {
                Timeout = TimeSpan.FromMilliseconds(douyinTimeout)
            };

            var headers = httpClient.DefaultRequestHeaders;
            headers.Add("User-Agent", HttpHeaderUserAgent);
            headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            headers.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            headers.Add("Referer", HttpHeaderReferer);

            logger.Debug("DouyinApiClient initialized with {Timeout}ms timeout (2x config value of {ConfigTimeout}ms)", douyinTimeout, config.TimingApiTimeout);
        }

        public async Task<PlatformRoomInfo> GetRoomInfoAsync(string roomIdentifier)
        {
            // Handle short URLs (v.douyin.com/xxx)
            if (roomIdentifier.Contains("v.douyin.com") || roomIdentifier.Contains("douyin.com/user/"))
            {
                var (roomId, _) = await ResolveShortUrlAsync(roomIdentifier);
                roomIdentifier = roomId;
            }

            // Use cached room info if available and fresh (within 30 seconds)
            if (cachedRoomInfo != null && (DateTime.UtcNow - cacheTime).TotalSeconds < 30)
            {
                logger.Debug("Using cached room info (age: {Age}s)", (DateTime.UtcNow - cacheTime).TotalSeconds);
                return cachedRoomInfo;
            }

            // Retry with exponential backoff (3 attempts: 0s, 2s, 4s delays)
            var maxRetries = 3;
            Exception? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        var delayMs = attempt * 2000; // 2s, 4s
                        logger.Debug("Retrying API request (attempt {Attempt}/{Max}) after {Delay}ms delay", attempt + 1, maxRetries, delayMs);
                        await Task.Delay(delayMs);
                    }

                    // Use api.douyin.wtf public API
                    var apiUrl = $"https://api.douyin.wtf/api/douyin/web/fetch_user_live_videos?webcast_id={roomIdentifier}";

                    var jsonStr = await this.httpClient.GetStringAsync(apiUrl);
                    var response = JObject.Parse(jsonStr);

                    // Check API response code
                    var code = response["code"]?.ToObject<int>();
                    if (code != 200)
                    {
                        throw new Exception($"API returned error code: {code}");
                    }

                    // Parse response: data.data.data[0]
                    var roomData = response["data"]?["data"]?["data"]?[0];
                    if (roomData == null)
                    {
                        throw new Exception("No room data in API response");
                    }

                    var userData = response["data"]?["data"]?["user"];
                    if (userData == null)
                    {
                        throw new Exception("No user data in API response");
                    }

                    var roomInfo = ParseRoomInfoFromApiResponse(roomData, roomIdentifier, userData);

                    // Cache the room info
                    this.cachedRoomInfo = roomInfo;
                    this.cacheTime = DateTime.UtcNow;

                    if (attempt > 0)
                    {
                        logger.Information("Successfully fetched room info after {Attempts} attempts", attempt + 1);
                    }

                    return roomInfo;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < maxRetries - 1)
                    {
                        logger.Debug(ex, "API request failed (attempt {Attempt}/{Max}), will retry", attempt + 1, maxRetries);
                    }
                }
            }

            logger.Warning(lastException, "Failed to get room info from api.douyin.wtf for room {RoomId} after {Attempts} attempts", roomIdentifier, maxRetries);
            throw new Exception($"Failed to get room info: {lastException?.Message}", lastException);
        }

        private PlatformRoomInfo ParseRoomInfoFromApiResponse(JToken roomData, string roomIdentifier, JToken userData = null)
        {
            var user = userData;
            var roomId = roomData["id_str"]?.ToString() ?? roomIdentifier;
            var title = roomData["title"]?.ToString() ?? "Unknown";
            var status = roomData["status"]?.ToObject<int>() ?? 0;
            var isLive = status == 2;

            long uid = 0;
            var anchorName = "Unknown";

            if (user != null)
            {
                anchorName = user?["nickname"]?.ToString() ?? "Unknown";
                var idStr = user?["id_str"]?.ToString();
                if (!string.IsNullOrEmpty(idStr) && long.TryParse(idStr, out var parsedId))
                {
                    uid = parsedId;
                }
            }

            var roomInfo = new PlatformRoomInfo
            {
                RoomId = roomId,
                AnchorName = anchorName,
                Title = title,
                IsLive = isLive,
                Uid = uid,
                AreaParent = "",
                AreaChild = "",
                CoverUrl = roomData["cover"]?["url_list"]?[0]?.ToString() ?? ""
            };

            // Only log at Information level if something actually changed
            if (this.lastLoggedRoomInfo == null ||
                this.lastLoggedRoomInfo.IsLive != roomInfo.IsLive ||
                this.lastLoggedRoomInfo.Title != roomInfo.Title ||
                this.lastLoggedRoomInfo.AnchorName != roomInfo.AnchorName)
            {
                logger.Information("Room info changed - Room: {RoomId}, Anchor: {Anchor}, Title: {Title}, Live: {IsLive}, UID: {Uid}",
                    roomId, anchorName, title, isLive, uid);
                this.lastLoggedRoomInfo = roomInfo;
            }

            return roomInfo;
        }

        public async Task<PlatformStreamInfo> GetStreamUrlAsync(string roomIdentifier, int qualityNumber)
        {
            // Handle short URLs
            if (roomIdentifier.Contains("v.douyin.com") || roomIdentifier.Contains("douyin.com/user/"))
            {
                var (roomId, _) = await ResolveShortUrlAsync(roomIdentifier);
                roomIdentifier = roomId;
            }

            // Retry with exponential backoff (3 attempts: 0s, 2s, 4s delays)
            var maxRetries = 3;
            Exception? lastException = null;

            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    if (attempt > 0)
                    {
                        var delayMs = attempt * 2000; // 2s, 4s
                        logger.Debug("Retrying stream URL request (attempt {Attempt}/{Max}) after {Delay}ms delay", attempt + 1, maxRetries, delayMs);
                        await Task.Delay(delayMs);
                    }

                    // Use api.douyin.wtf public API
                    var apiUrl = $"https://api.douyin.wtf/api/douyin/web/fetch_user_live_videos?webcast_id={roomIdentifier}";

                    var jsonStr = await this.httpClient.GetStringAsync(apiUrl);
                    var response = JObject.Parse(jsonStr);

                    // Check API response code
                    var code = response["code"]?.ToObject<int>();
                    if (code != 200)
                    {
                        throw new Exception($"API returned error code: {code}");
                    }

                    // Parse response: data.data.data[0]
                    var roomData = response["data"]?["data"]?["data"]?[0];
                    if (roomData == null)
                    {
                        throw new Exception("No room data in API response");
                    }

                    // Check if stream is live
                    var status = roomData["status"]?.ToObject<int>() ?? 0;
                    if (status != 2)
                    {
                        throw new Exception("Stream is offline");
                    }

                    // Cache the room info for GetRoomInfoAsync to use
                    var roomInfo = ParseRoomInfoFromApiResponse(roomData, roomIdentifier);
                    this.cachedRoomInfo = roomInfo;
                    this.cacheTime = DateTime.UtcNow;

                    // Extract stream URL from API response
                    var streamInfo = SelectStreamUrlFromApiResponse(roomData, qualityNumber);

                    if (attempt > 0)
                    {
                        logger.Information("Successfully fetched stream URL after {Attempts} attempts", attempt + 1);
                    }

                    return streamInfo;
                }
                catch (Exception ex)
                {
                    lastException = ex;

                    if (attempt < maxRetries - 1)
                    {
                        logger.Debug(ex, "Stream URL request failed (attempt {Attempt}/{Max}), will retry", attempt + 1, maxRetries);
                    }
                }
            }

            logger.Warning(lastException, "Failed to get stream URL from api.douyin.wtf for room {RoomId} after {Attempts} attempts", roomIdentifier, maxRetries);
            throw new Exception($"Failed to get stream URL: {lastException?.Message}", lastException);
        }

        private PlatformRoomInfo? cachedRoomInfo;
        private DateTime cacheTime;
        private PlatformRoomInfo? lastLoggedRoomInfo;

        private PlatformRoomInfo ParseRoomInfoFromHtml(string html)
        {
            try
            {
                // Extract JSON data from HTML
                var match = Regex.Match(html, @"(\{\\""state\\"":.*?)\]\\n""\]");
                if (!match.Success)
                {
                    match = Regex.Match(html, @"(\{\\""common\\"":.*?)\]\\n""\]</script><div hidden");
                }

                if (!match.Success)
                {
                    throw new Exception("Failed to extract room data from HTML");
                }

                var jsonStr = match.Groups[1].Value
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\u0026", "&");

                // Extract ALL roomStore occurrences (there may be multiple, first one is usually empty)
                var roomStoreMatches = Regex.Matches(jsonStr, @"""roomStore"":\{""store"":[^,]*,""roomInfo"":(.*?)(?:,""emojiList"")", RegexOptions.Singleline);

                if (roomStoreMatches.Count == 0)
                {
                    throw new Exception("Failed to extract roomStore from JSON");
                }

                JObject? jsonData = null;
                string anchorName = "Unknown";

                // Try to find a non-empty roomInfo from matches
                foreach (Match roomStoreMatch in roomStoreMatches)
                {
                    var roomInfoJson = roomStoreMatch.Groups[1].Value;

                    // Skip if roomInfo is empty
                    if (roomInfoJson.Trim() == "{}")
                    {
                        logger.Debug("Skipping empty roomInfo");
                        continue;
                    }

                    try
                    {
                        var roomInfoObj = JObject.Parse(roomInfoJson);
                        logger.Debug("Parsed roomInfo object, keys: {Keys}", string.Join(", ", roomInfoObj.Properties().Select(p => p.Name)));

                        var roomObj = roomInfoObj["room"] as JObject;

                        if (roomObj != null && roomObj.Count > 0)
                        {
                            jsonData = roomObj;
                            logger.Debug("Found room object with keys: {Keys}", string.Join(", ", roomObj.Properties().Select(p => p.Name)));

                            // Extract anchor name from owner
                            anchorName = roomObj["owner"]?["nickname"]?.ToString() ?? "Unknown";
                            logger.Debug("Extracted anchor name from roomObj[\"owner\"]: {Name}", anchorName);
                            break;
                        }
                        else
                        {
                            logger.Debug("roomInfo has no 'room' key or it's empty. Trying to use roomInfo directly as room data.");
                            // Sometimes the roomInfo itself IS the room data (no nested "room" key)
                            if (roomInfoObj.ContainsKey("id_str") || roomInfoObj.ContainsKey("id") || roomInfoObj.ContainsKey("title"))
                            {
                                jsonData = roomInfoObj;
                                logger.Debug("Using roomInfo directly as room data, keys: {Keys}", string.Join(", ", roomInfoObj.Properties().Select(p => p.Name)));

                                // Extract anchor name
                                anchorName = roomInfoObj["owner"]?["nickname"]?.ToString() ?? "Unknown";
                                logger.Debug("Extracted anchor name from roomInfo[\"owner\"]: {Name}", anchorName);
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Failed to parse roomInfo match, trying next");
                        // Try next match
                        continue;
                    }
                }

                if (jsonData == null)
                {
                    throw new Exception("Failed to parse room data from JSON - all roomInfo objects are empty");
                }

                // Debug logging for owner information
                var owner = jsonData["owner"];
                if (owner == null)
                {
                    logger.Warning("No owner information found in room data. Room data keys: {Keys}", string.Join(", ", jsonData.Properties().Select(p => p.Name)));
                }
                else
                {
                    logger.Debug("Owner data found: {Owner}", owner.ToString(Newtonsoft.Json.Formatting.None));
                }

                var roomId = jsonData["id_str"]?.ToString() ?? jsonData["id"]?.ToString() ?? "";
                var uid = 0L;

                // Try to get UID from owner object
                if (owner != null)
                {
                    // Try different possible UID field names
                    // Try id_str first (string that needs parsing), then numeric fields
                    var idStr = owner["id_str"]?.ToString();
                    if (!string.IsNullOrEmpty(idStr) && long.TryParse(idStr, out var parsedId))
                    {
                        uid = parsedId;
                    }
                    else
                    {
                        uid = owner["id"]?.ToObject<long>() ??
                              owner["uid"]?.ToObject<long>() ??
                              owner["user_id"]?.ToObject<long>() ?? 0;
                    }

                    // Also try to get anchor name from owner if not already set
                    if (anchorName == "Unknown")
                    {
                        anchorName = owner["nickname"]?.ToString() ??
                                   owner["name"]?.ToString() ??
                                   owner["display_name"]?.ToString() ?? "Unknown";
                    }
                }

                return new PlatformRoomInfo
                {
                    RoomId = roomId,
                    AnchorName = anchorName,
                    Title = jsonData["title"]?.ToString() ?? "",
                    IsLive = jsonData["status"]?.ToObject<int>() == 2,
                    Uid = uid,
                    CoverUrl = jsonData["cover"]?["url_list"]?[0]?.ToString() ?? "",
                    AreaParent = "",
                    AreaChild = ""
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to parse room info: {ex.Message}", ex);
            }
        }

        private JObject ExtractStreamDataFromHtml(string html)
        {
            try
            {
                var match = Regex.Match(html, @"(\{\\""state\\"":.*?)\]\\n""\]");
                if (!match.Success)
                {
                    match = Regex.Match(html, @"(\{\\""common\\"":.*?)\]\\n""\]</script><div hidden");
                }

                if (!match.Success)
                {
                    throw new Exception("Failed to extract stream data from HTML");
                }

                var jsonStr = match.Groups[1].Value
                    .Replace("\\\"", "\"")
                    .Replace("\\\\", "\\")
                    .Replace("\\u0026", "&");

                // Extract ALL roomStore occurrences (there may be multiple, first one is usually empty)
                var roomStoreMatches = Regex.Matches(jsonStr, @"""roomStore"":\{""store"":[^,]*,""roomInfo"":(.*?)(?:,""emojiList"")", RegexOptions.Singleline);

                if (roomStoreMatches.Count == 0)
                {
                    throw new Exception("Failed to extract roomStore from JSON");
                }

                // Try to find a non-empty roomInfo from matches
                foreach (Match roomStoreMatch in roomStoreMatches)
                {
                    var roomInfoJson = roomStoreMatch.Groups[1].Value;

                    // Skip if roomInfo is empty
                    if (roomInfoJson.Trim() == "{}")
                    {
                        continue;
                    }

                    try
                    {
                        var roomInfoObj = JObject.Parse(roomInfoJson);
                        if (roomInfoObj.Count > 0)
                        {
                            return roomInfoObj;
                        }
                    }
                    catch
                    {
                        // Try next match
                        continue;
                    }
                }

                throw new Exception("Failed to parse roomInfo from JSON - all roomInfo objects are empty");
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to extract stream data: {ex.Message}", ex);
            }
        }

        private PlatformStreamInfo SelectStreamUrlFromApiResponse(JToken roomData, int qualityNumber)
        {
            var streamUrl = roomData["stream_url"];

            if (streamUrl == null)
            {
                logger.Warning("No stream_url found in API response. Room data: {Data}", roomData.ToString(Newtonsoft.Json.Formatting.Indented));
                throw new Exception("No stream URL available");
            }

            // Try to get FLV and HLS URLs
            var flvUrlMap = streamUrl["flv_pull_url"] as JObject;
            var hlsUrlMap = streamUrl["hls_pull_url_map"] as JObject;

            // Douyin uses keys like FULL_HD1, HD1, SD1, SD2 for qualities
            // Prefer HD1/SD1 over FULL_HD1 to get 30fps instead of 60fps (saves bandwidth and encoding time)
            // FULL_HD1 is typically 60fps with high bitrate, HD1/SD1 are usually 30fps
            var qualityKeys = new[] { "HD1", "SD1", "FULL_HD1", "SD2", "LD1" };

            // Try FLV first (better for direct streaming without FFmpeg)
            if (flvUrlMap != null)
            {
                foreach (var key in qualityKeys)
                {
                    if (flvUrlMap[key] != null)
                    {
                        var url = flvUrlMap[key].ToString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            logger.Information("使用抖音 FLV 流，画质: {Quality} (优先选择 30fps 以节省带宽)", key);
                            return new PlatformStreamInfo
                            {
                                StreamUrl = url,
                                Quality = key,
                                Format = StreamFormat.FLV,
                                Codec = "h264",
                                QualityNumber = MapDouyinQualityToNumber(key)
                            };
                        }
                    }
                }
            }

            // HLS as fallback (requires FFmpeg or special handling)
            if (hlsUrlMap != null)
            {
                foreach (var key in qualityKeys)
                {
                    if (hlsUrlMap[key] != null)
                    {
                        var url = hlsUrlMap[key].ToString();
                        if (!string.IsNullOrEmpty(url))
                        {
                            logger.Information("使用抖音 HLS 流，画质: {Quality} (优先选择 30fps 以节省带宽)", key);
                            return new PlatformStreamInfo
                            {
                                StreamUrl = url,
                                Quality = key,
                                Format = StreamFormat.HLS,
                                Codec = "h264",
                                QualityNumber = MapDouyinQualityToNumber(key)
                            };
                        }
                    }
                }
            }

            throw new Exception("No available stream URL found for any quality");
        }

        private string MapQualityNumberToDouyinQuality(int qualityNumber)
        {
            // Map Bilibili-style quality numbers to Douyin quality strings
            return qualityNumber switch
            {
                >= 10000 => "ORIGIN",
                >= 400 => "BD",
                >= 250 => "UHD",
                >= 150 => "HD",
                >= 80 => "SD",
                _ => "LD"
            };
        }

        private int MapDouyinQualityToNumber(string douyinQuality)
        {
            // Map Douyin quality keys to quality numbers
            return douyinQuality switch
            {
                "FULL_HD1" => 10000,  // Original/Blu-ray
                "HD1" => 400,          // High definition
                "SD1" => 150,          // Standard definition
                "SD2" => 80,           // Lower SD
                "LD1" => 80,           // Lowest definition
                _ => 150
            };
        }

        private async Task<(string roomId, string secUserId)> ResolveShortUrlAsync(string shortUrl)
        {
            try
            {
                // Ensure URL has protocol
                if (!shortUrl.StartsWith("http"))
                {
                    shortUrl = "https://" + shortUrl;
                }

                var response = await this.httpClient.GetAsync(shortUrl);
                var redirectUrl = response.RequestMessage?.RequestUri?.ToString() ?? "";

                // Extract sec_user_id from redirect URL
                var secUserIdMatch = Regex.Match(redirectUrl, @"sec_user_id=([\w_\-]+)");
                var secUserId = secUserIdMatch.Success ? secUserIdMatch.Groups[1].Value : "";

                // Extract room ID from redirect URL
                var roomIdMatch = Regex.Match(redirectUrl, @"live\.douyin\.com/(\d+)");
                if (!roomIdMatch.Success)
                {
                    // Try to extract from path
                    var uri = new Uri(redirectUrl);
                    var segments = uri.AbsolutePath.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();
                    if (segments.Length > 0)
                    {
                        var lastSegment = segments[segments.Length - 1];
                        if (long.TryParse(lastSegment, out _))
                        {
                            return (lastSegment, secUserId);
                        }
                    }

                    throw new Exception("Failed to extract room ID from redirect URL");
                }

                var roomId = roomIdMatch.Groups[1].Value;
                return (roomId, secUserId);
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to resolve short URL: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            if (!this.disposedValue)
            {
                this.httpClient?.Dispose();
                this.disposedValue = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
