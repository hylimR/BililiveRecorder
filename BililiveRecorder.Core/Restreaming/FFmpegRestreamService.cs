using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace BililiveRecorder.Core.Restreaming
{
    /// <summary>
    /// 基于FFmpeg的转推服务实现
    /// </summary>
    public class FFmpegRestreamService : IRestreamService
    {
        private readonly ILogger logger;
        private Process? ffmpegProcess;
        private Stream? ffmpegInputStream;
        private readonly object processLock = new object();
        private bool disposed = false;

        public FFmpegRestreamService(ILogger logger)
        {
            this.logger = logger?.ForContext<FFmpegRestreamService>() ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsRunning => this.ffmpegProcess != null && !this.ffmpegProcess.HasExited;

        public event EventHandler<RestreamErrorEventArgs>? ProcessExited;

        public Task StartAsync(string rtmpUrl, string streamKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rtmpUrl))
                throw new ArgumentException("RTMP URL cannot be empty", nameof(rtmpUrl));

            if (string.IsNullOrWhiteSpace(streamKey))
                throw new ArgumentException("Stream key cannot be empty", nameof(streamKey));

            lock (this.processLock)
            {
                if (this.IsRunning)
                {
                    this.logger.Warning("转推服务已经在运行，无法重复启动");
                    return Task.CompletedTask;
                }

                var fullRtmpUrl = rtmpUrl.TrimEnd('/') + "/" + streamKey.TrimStart('/');

                // FFmpeg 参数:
                // -re: 以实时速率读取输入
                // -i pipe:0: 从标准输入读取
                // -c copy: 复制编码，不重新编码（性能最优）
                // -f flv: 输出格式为FLV
                // -y: 覆盖输出文件
                var ffmpegArgs = $"-re -i pipe:0 -c copy -f flv \"{fullRtmpUrl}\"";

                this.logger.Information("启动FFmpeg转推进程，目标: {RtmpUrl}", rtmpUrl);
                this.logger.Debug("FFmpeg 参数: {Args}", ffmpegArgs);

                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArgs,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    this.ffmpegProcess = new Process { StartInfo = startInfo };
                    this.ffmpegProcess.EnableRaisingEvents = true;
                    this.ffmpegProcess.Exited += this.FfmpegProcess_Exited;

                    // 异步读取stderr输出以记录日志
                    this.ffmpegProcess.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            this.logger.Debug("[FFmpeg] {Output}", e.Data);
                        }
                    };

                    this.ffmpegProcess.Start();
                    this.ffmpegProcess.BeginErrorReadLine();

                    this.ffmpegInputStream = this.ffmpegProcess.StandardInput.BaseStream;

                    this.logger.Information("FFmpeg转推进程启动成功，进程ID: {ProcessId}", this.ffmpegProcess.Id);
                }
                catch (Exception ex)
                {
                    this.logger.Error(ex, "启动FFmpeg转推进程失败");
                    this.Cleanup();
                    throw;
                }
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            lock (this.processLock)
            {
                if (this.ffmpegProcess == null || this.ffmpegProcess.HasExited)
                {
                    this.logger.Debug("转推服务未运行，无需停止");
                    return;
                }

                this.logger.Information("停止FFmpeg转推进程");

                try
                {
                    // 关闭stdin以让FFmpeg正常退出
                    this.ffmpegInputStream?.Close();
                    this.ffmpegInputStream = null;
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "关闭FFmpeg输入流时发生错误");
                }
            }

            // 等待进程退出（最多5秒）
            try
            {
                var exitTask = Task.Run(() =>
                {
                    this.ffmpegProcess?.WaitForExit(5000);
                });

                await exitTask.ConfigureAwait(false);

                if (this.ffmpegProcess != null && !this.ffmpegProcess.HasExited)
                {
                    this.logger.Warning("FFmpeg进程未在5秒内退出，强制结束");
                    this.ffmpegProcess.Kill();
                }
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "等待FFmpeg进程退出时发生错误");
            }
            finally
            {
                this.Cleanup();
            }
        }

        public Stream GetInputStream()
        {
            if (this.ffmpegInputStream == null)
                throw new InvalidOperationException("转推服务未启动");

            return this.ffmpegInputStream;
        }

        private void FfmpegProcess_Exited(object? sender, EventArgs e)
        {
            var exitCode = this.ffmpegProcess?.ExitCode ?? -1;

            if (exitCode != 0)
            {
                this.logger.Warning("FFmpeg转推进程异常退出，退出码: {ExitCode}", exitCode);
                this.ProcessExited?.Invoke(this, new RestreamErrorEventArgs
                {
                    ExitCode = exitCode,
                    ErrorMessage = $"FFmpeg进程异常退出，退出码: {exitCode}"
                });
            }
            else
            {
                this.logger.Information("FFmpeg转推进程正常退出");
            }

            this.Cleanup();
        }

        private void Cleanup()
        {
            try
            {
                if (this.ffmpegProcess != null)
                {
                    this.ffmpegProcess.Exited -= this.FfmpegProcess_Exited;

                    if (!this.ffmpegProcess.HasExited)
                    {
                        try { this.ffmpegProcess.Kill(); } catch { }
                    }

                    this.ffmpegProcess.Dispose();
                    this.ffmpegProcess = null;
                }

                this.ffmpegInputStream = null;
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "清理FFmpeg进程资源时发生错误");
            }
        }

        public void Dispose()
        {
            if (this.disposed)
                return;

            this.disposed = true;

            try
            {
                this.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "释放转推服务资源时发生错误");
            }
        }
    }
}
