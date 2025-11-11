using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BililiveRecorder.Core.Restreaming
{
    /// <summary>
    /// 转推服务接口
    /// </summary>
    public interface IRestreamService : IDisposable
    {
        /// <summary>
        /// 转推是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 启动转推
        /// </summary>
        /// <param name="rtmpUrl">RTMP推流地址</param>
        /// <param name="streamKey">推流密钥</param>
        /// <param name="cancellationToken">取消令牌</param>
        Task StartAsync(string rtmpUrl, string streamKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// 停止转推
        /// </summary>
        Task StopAsync();

        /// <summary>
        /// 获取用于写入流数据的Stream
        /// </summary>
        Stream GetInputStream();

        /// <summary>
        /// FFmpeg进程退出事件
        /// </summary>
        event EventHandler<RestreamErrorEventArgs>? ProcessExited;
    }

    /// <summary>
    /// 转推错误事件参数
    /// </summary>
    public class RestreamErrorEventArgs : EventArgs
    {
        public int ExitCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
