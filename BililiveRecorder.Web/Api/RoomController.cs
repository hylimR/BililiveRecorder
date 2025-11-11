using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AutoMapper;
using BililiveRecorder.Core;
using BililiveRecorder.Web.Models;
using BililiveRecorder.Web.Models.Rest;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace BililiveRecorder.Web.Api
{
    [ApiController, Route("api/[controller]", Name = "[controller] [action]")]
    public sealed class RoomController : ControllerBase
    {
        private readonly IMapper mapper;
        private readonly IRecorder recorder;

        public RoomController(IMapper mapper, IRecorder recorder)
        {
            this.mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            this.recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IRoom? FetchRoom(long roomId) => this.recorder.Rooms.FirstOrDefault(x => x.ShortId == roomId || x.RoomConfig.RoomId == roomId);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private IRoom? FetchRoom(Guid objectId) => this.recorder.Rooms.FirstOrDefault(x => x.ObjectId == objectId);

        /// <summary>
        /// 列出所有直播间
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public RoomDto[] GetRooms() => this.mapper.Map<RoomDto[]>(this.recorder.Rooms);

        #region Create & Delete

        /// <summary>
        /// 添加直播间
        /// </summary>
        /// <param name="createRoom"></param>
        /// <returns></returns>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status400BadRequest)]
        public ActionResult<RoomDto> CreateRoom([FromBody] CreateRoomDto createRoom)
        {
            IRoom? room = null;
            var input = createRoom.RoomUrl?.Trim();

            if (string.IsNullOrWhiteSpace(input) && !createRoom.RoomId.HasValue)
            {
                return this.BadRequest(new RestApiError { Code = RestApiErrorCode.RoomidOutOfRange, Message = "Either RoomId or RoomUrl must be provided." });
            }

            long? parsedRoomId = null;
            string? originalUrl = null;

            // Parse the input to extract room ID and preserve original URL
            if (!string.IsNullOrWhiteSpace(input))
            {
                originalUrl = input; // Store the original URL as provided by user

                // Try to parse as integer first (for plain numeric input - assume Bilibili)
                if (long.TryParse(input, out var roomIdFromString))
                {
                    if (roomIdFromString <= 0)
                        return this.BadRequest(new RestApiError { Code = RestApiErrorCode.RoomidOutOfRange, Message = "Roomid must be greater than 0." });

                    parsedRoomId = roomIdFromString;
                    // For plain numeric input, construct a proper Bilibili URL
                    originalUrl = $"https://live.bilibili.com/{roomIdFromString}";
                }
                else
                {
                    // Try to extract Bilibili room ID from URL
                    var bilibiliMatch = RoomIdFromUrl.Regex.Match(input);
                    if (bilibiliMatch.Success && bilibiliMatch.Groups.Count > 1 && long.TryParse(bilibiliMatch.Groups[1].Value, out var bilibiliRoomId))
                    {
                        // It's a Bilibili URL - extract room ID but keep original URL
                        parsedRoomId = bilibiliRoomId;
                        // originalUrl already set to input
                    }
                    else
                    {
                        // Try to extract Douyin room ID from various URL formats
                        var douyinPatterns = new[]
                        {
                            @"live\.douyin\.com/(\d+)", // https://live.douyin.com/123456789
                            @"v\.douyin\.com/[^/]+.*?roomId[=:](\d+)", // v.douyin.com/xxx?roomId=123
                            @"webcast\.amemv\.com.*?room_id[=:](\d+)", // webcast.amemv.com/xxx?room_id=123
                            @"douyin\.com.*?roomId[=:](\d+)" // any douyin.com URL with roomId parameter
                        };

                        foreach (var pattern in douyinPatterns)
                        {
                            var douyinMatch = System.Text.RegularExpressions.Regex.Match(input, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (douyinMatch.Success && douyinMatch.Groups.Count > 1 && long.TryParse(douyinMatch.Groups[1].Value, out var douyinRoomId))
                            {
                                // It's a Douyin URL with numeric room ID - extract room ID but keep original URL
                                parsedRoomId = douyinRoomId;
                                // originalUrl already set to input
                                break;
                            }
                        }

                        // If no room ID extracted, that's okay - we'll use the URL as-is
                        // This handles cases like v.douyin.com/xxx short URLs without extractable IDs
                    }
                }
            }
            // Fallback to RoomId if provided directly (legacy support)
            else if (createRoom.RoomId.HasValue)
            {
                if (createRoom.RoomId.Value <= 0)
                    return this.BadRequest(new RestApiError { Code = RestApiErrorCode.RoomidOutOfRange, Message = "Roomid must be greater than 0." });

                parsedRoomId = createRoom.RoomId.Value;
                originalUrl = $"https://live.bilibili.com/{createRoom.RoomId.Value}";
            }

            // Check for existing room by room ID or URL
            if (parsedRoomId.HasValue)
            {
                room = this.FetchRoom(parsedRoomId.Value);
            }

            if (room == null && !string.IsNullOrWhiteSpace(originalUrl))
            {
                room = this.recorder.Rooms.FirstOrDefault(x =>
                    x.RoomConfig.RoomUrl == originalUrl ||
                    x.RoomConfig.NormalizedRoomIdentifier == originalUrl);
            }

            // Update existing room or create new one
            if (room is not null)
            {
                // Update AutoRecord setting if changed
                if (room.RoomConfig.AutoRecord != createRoom.AutoRecord)
                    room.RoomConfig.AutoRecord = createRoom.AutoRecord;

                // Update RoomUrl if it's not already set - store the original URL
                if (string.IsNullOrEmpty(room.RoomConfig.RoomUrl) && !string.IsNullOrWhiteSpace(originalUrl))
                    room.RoomConfig.RoomUrl = originalUrl;

                // Update RoomId if it's not already set and we have a parsed ID
                if (room.RoomConfig.RoomId == 0 && parsedRoomId.HasValue)
                    room.RoomConfig.RoomId = parsedRoomId.Value;

                this.recorder.SaveConfig();
            }
            else
            {
                // Create new room - prefer using string URL for multi-platform support
                try
                {
                    if (!string.IsNullOrWhiteSpace(originalUrl))
                    {
                        // Use URL-based AddRoom with original URL
                        room = this.recorder.AddRoom(originalUrl, createRoom.AutoRecord);

                        // Then set RoomId if we have a numeric ID extracted from URL
                        if (parsedRoomId.HasValue && room.RoomConfig.RoomId == 0)
                        {
                            room.RoomConfig.RoomId = parsedRoomId.Value;
                            this.recorder.SaveConfig();
                        }
                    }
                    else if (parsedRoomId.HasValue)
                    {
                        // Fallback to ID-based AddRoom (legacy, for Bilibili only)
                        room = this.recorder.AddRoom(parsedRoomId.Value, createRoom.AutoRecord);

                        // Then set RoomUrl with original URL
                        if (!string.IsNullOrWhiteSpace(originalUrl) && string.IsNullOrEmpty(room.RoomConfig.RoomUrl))
                        {
                            room.RoomConfig.RoomUrl = originalUrl;
                            this.recorder.SaveConfig();
                        }
                    }
                    else
                    {
                        return this.BadRequest(new RestApiError { Code = RestApiErrorCode.RoomidOutOfRange, Message = "Could not parse room identifier from input." });
                    }
                }
                catch (System.Exception ex)
                {
                    return this.BadRequest(new RestApiError { Code = RestApiErrorCode.RoomidOutOfRange, Message = $"Failed to add room: {ex.Message}" });
                }
            }

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 删除直播间
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpDelete("{roomId:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> DeleteRoom(long roomId)
        {
            var room = this.FetchRoom(roomId);

            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            this.recorder.RemoveRoom(room);

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 删除直播间
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpDelete("{objectId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> DeleteRoom(Guid objectId)
        {
            var room = this.FetchRoom(objectId);

            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            this.recorder.RemoveRoom(room);

            return this.mapper.Map<RoomDto>(room);
        }

        #endregion
        #region Get Room

        /// <summary>
        /// 读取一个直播间
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpGet("{roomId:long}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> GetRoom(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 读取一个直播间
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpGet("{objectId:guid}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> GetRoom(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomDto>(room);
        }

        #endregion
        #region Get Room Stats

        /// <summary>
        /// 读取直播间录制统计信息
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpGet("{roomId:long}/stats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomRecordingStatsDto> GetRoomRecordingStats(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomRecordingStatsDto>(room.Stats);
        }

        /// <summary>
        /// 读取直播间录制统计信息
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpGet("{objectId:guid}/stats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomRecordingStatsDto> GetRoomRecordingStats(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomRecordingStatsDto>(room.Stats);
        }

        /// <summary>
        /// 读取直播间 IO 统计信息
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpGet("{roomId:long}/ioStats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomIOStatsDto> GetRoomIOStats(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomIOStatsDto>(room.Stats);
        }

        /// <summary>
        /// 读取直播间 IO 统计信息
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpGet("{objectId:guid}/ioStats")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomIOStatsDto> GetRoomIOStats(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomIOStatsDto>(room.Stats);
        }

        #endregion
        #region Room Config

        /// <summary>
        /// 读取直播间设置
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpGet("{roomId:long}/config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomConfigDto> GetRoomConfig(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomConfigDto>(room.RoomConfig);
        }

        /// <summary>
        /// 读取直播间设置
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpGet("{objectId:guid}/config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomConfigDto> GetRoomConfig(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });
            return this.mapper.Map<RoomConfigDto>(room.RoomConfig);
        }

        /// <summary>
        /// 修改直播间设置
        /// </summary>
        /// <param name="roomId"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPost("{roomId:long}/config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomConfigDto> SetRoomConfig(long roomId, [FromBody] SetRoomConfig config)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            config.ApplyTo(room.RoomConfig);

            this.recorder.SaveConfig();

            return this.mapper.Map<RoomConfigDto>(room.RoomConfig);
        }

        /// <summary>
        /// 修改直播间设置
        /// </summary>
        /// <param name="objectId"></param>
        /// <param name="config"></param>
        /// <returns></returns>
        [HttpPost("{objectId:guid}/config")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomConfigDto> SetRoomConfig(Guid objectId, [FromBody] SetRoomConfig config)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            config.ApplyTo(room.RoomConfig);

            this.recorder.SaveConfig();

            return this.mapper.Map<RoomConfigDto>(room.RoomConfig);
        }

        #endregion
        #region Room Action

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpPost("{roomId:long}/start")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> StartRecording(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            room.StartRecord();

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 开始录制
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpPost("{objectId:guid}/start")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> StartRecording(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            room.StartRecord();

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpPost("{roomId:long}/stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> StopRecording(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            room.StopRecord();

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 停止录制
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpPost("{objectId:guid}/stop")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> StopRecording(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            room.StopRecord();

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 手动分段
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpPost("{roomId:long}/split")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> SplitRecording(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            room.SplitOutput();

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 手动分段
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpPost("{objectId:guid}/split")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public ActionResult<RoomDto> SplitRecording(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            room.SplitOutput();

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 刷新直播间信息
        /// </summary>
        /// <param name="roomId"></param>
        /// <returns></returns>
        [HttpPost("{roomId:long}/refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RoomDto>> RefreshRecordingAsync(long roomId)
        {
            var room = this.FetchRoom(roomId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            await room.RefreshRoomInfoAsync().ConfigureAwait(false);

            return this.mapper.Map<RoomDto>(room);
        }

        /// <summary>
        /// 刷新直播间信息
        /// </summary>
        /// <param name="objectId"></param>
        /// <returns></returns>
        [HttpPost("{objectId:guid}/refresh")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(RestApiError), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<RoomDto>> RefreshRecordingAsync(Guid objectId)
        {
            var room = this.FetchRoom(objectId);
            if (room is null)
                return this.NotFound(new RestApiError { Code = RestApiErrorCode.RoomNotFound, Message = "Room not found" });

            await room.RefreshRoomInfoAsync().ConfigureAwait(false);

            return this.mapper.Map<RoomDto>(room);
        }

        #endregion
    }
}
