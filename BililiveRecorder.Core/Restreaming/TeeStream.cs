using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace BililiveRecorder.Core.Restreaming
{
    /// <summary>
    /// 一个将数据同时写入两个目标Stream的包装类（类似Unix的tee命令）
    /// </summary>
    internal class TeeStream : Stream
    {
        private readonly Stream primaryStream;
        private readonly Stream secondaryStream;
        private readonly bool leaveOpen;

        public TeeStream(Stream primaryStream, Stream secondaryStream, bool leaveOpen = false)
        {
            this.primaryStream = primaryStream ?? throw new ArgumentNullException(nameof(primaryStream));
            this.secondaryStream = secondaryStream ?? throw new ArgumentNullException(nameof(secondaryStream));
            this.leaveOpen = leaveOpen;
        }

        public override bool CanRead => this.primaryStream.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => this.primaryStream.CanWrite && this.secondaryStream.CanWrite;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            this.primaryStream.Flush();
            try { this.secondaryStream.Flush(); } catch { }
        }

        public override async Task FlushAsync(CancellationToken cancellationToken)
        {
            await this.primaryStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            try { await this.secondaryStream.FlushAsync(cancellationToken).ConfigureAwait(false); } catch { }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.primaryStream.Read(buffer, offset, count);
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var bytesRead = await this.primaryStream.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);

            if (bytesRead > 0)
            {
                // 同时写入到转推流
                try
                {
                    await this.secondaryStream.WriteAsync(buffer, offset, bytesRead, cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // 如果转推失败，不影响录制
                }
            }

            return bytesRead;
        }

#if NET6_0_OR_GREATER
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await this.primaryStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);

            if (bytesRead > 0)
            {
                // 同时写入到转推流
                try
                {
                    await this.secondaryStream.WriteAsync(buffer.Slice(0, bytesRead), cancellationToken).ConfigureAwait(false);
                }
                catch
                {
                    // 如果转推失败，不影响录制
                }
            }

            return bytesRead;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("TeeStream is read-only");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !this.leaveOpen)
            {
                try { this.primaryStream.Dispose(); } catch { }
                try { this.secondaryStream.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

#if NET6_0_OR_GREATER
        public override async ValueTask DisposeAsync()
        {
            if (!this.leaveOpen)
            {
                try { await this.primaryStream.DisposeAsync().ConfigureAwait(false); } catch { }
                try { await this.secondaryStream.DisposeAsync().ConfigureAwait(false); } catch { }
            }
            await base.DisposeAsync().ConfigureAwait(false);
        }
#endif
    }
}
