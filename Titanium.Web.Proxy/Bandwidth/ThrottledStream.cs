using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Titanium.Web.Proxy.Bandwidth
{
    /// <summary>
    /// Stream wrapper để kiểm soát tốc độ truyền dữ liệu
    /// </summary>
    public class ThrottledStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _bytesPerSecond;
        private readonly Stopwatch _stopwatch;
        private long _byteCount;
        
        public long TotalBytesTransferred { get; private set; }

        public ThrottledStream(Stream baseStream, long bytesPerSecond)
        {
            _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
            _bytesPerSecond = bytesPerSecond > 0 ? bytesPerSecond : long.MaxValue;
            _stopwatch = Stopwatch.StartNew();
            _byteCount = 0;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position 
        {
            get => _baseStream.Position;
            set => _baseStream.Position = value;
        }

        public override void Flush() => _baseStream.Flush();
        public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);
        public override void SetLength(long value) => _baseStream.SetLength(value);

        private void ThrottleTransfer(int bytesToTransfer)
        {
            if (_bytesPerSecond == long.MaxValue)
                return;

            _byteCount += bytesToTransfer;
            long elapsedMilliseconds = _stopwatch.ElapsedMilliseconds;
            
            // Nếu đã truyền được 1 giây hoặc hơn, reset bộ đếm
            if (elapsedMilliseconds >= 1000)
            {
                _byteCount = bytesToTransfer;
                _stopwatch.Restart();
                return;
            }

            // Tính toán số byte tối đa được phép trong khoảng thời gian đã trôi qua
            long maxBytesPerElapsedTime = _bytesPerSecond * elapsedMilliseconds / 1000;
            
            if (_byteCount > maxBytesPerElapsedTime)
            {
                // Tính toán thời gian cần ngủ để đạt đúng tốc độ
                int sleepTime = (int)(1000 * (_byteCount - maxBytesPerElapsedTime) / _bytesPerSecond);
                if (sleepTime > 0)
                {
                    Thread.Sleep(sleepTime);
                }
                
                // Reset bộ đếm sau khi ngủ
                _byteCount = bytesToTransfer;
                _stopwatch.Restart();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ThrottleTransfer(count);
            int bytesRead = _baseStream.Read(buffer, offset, count);
            TotalBytesTransferred += bytesRead;
            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            ThrottleTransfer(count);
            _baseStream.Write(buffer, offset, count);
            TotalBytesTransferred += count;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrottleTransfer(count);
            int bytesRead = await _baseStream.ReadAsync(buffer, offset, count, cancellationToken);
            TotalBytesTransferred += bytesRead;
            return bytesRead;
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ThrottleTransfer(count);
            await _baseStream.WriteAsync(buffer, offset, count, cancellationToken);
            TotalBytesTransferred += count;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _baseStream.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
