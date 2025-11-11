namespace BililiveRecorder.Web.Models.Rest
{
    public class CreateRoomDto
    {
        /// <summary>
        /// 房间号 (仅 Bilibili, 可选)
        /// </summary>
        public long? RoomId { get; set; }

        /// <summary>
        /// 房间URL (支持 Bilibili 和 Douyin)
        /// 示例: "https://live.bilibili.com/123456" 或 "https://live.douyin.com/123456789"
        /// </summary>
        public string? RoomUrl { get; set; }

        /// <summary>
        /// 是否自动录制
        /// </summary>
        public bool AutoRecord { get; set; }
    }
}
