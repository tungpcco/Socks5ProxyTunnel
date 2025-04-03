using System;
using System.Threading;

namespace Titanium.Web.Proxy.Bandwidth
{
    /// <summary>
    /// Quản lý băng thông và theo dõi lưu lượng truy cập
    /// </summary>
    public class BandwidthManager
    {
        // Các biến kiểm soát tốc độ
        public long DownloadSpeedLimit { get; set; } = -1; // -1 là không giới hạn
        public long UploadSpeedLimit { get; set; } = -1; // -1 là không giới hạn
        
        // Các biến kiểm soát dung lượng tối đa
        public long DownloadVolumeLimit { get; set; } = -1; // -1 là không giới hạn
        public long UploadVolumeLimit { get; set; } = -1; // -1 là không giới hạn
        
        // Cờ để biết khi nào đã vượt quá giới hạn
        public bool IsDownloadVolumeLimitExceeded { get; set; } = false;
        public bool IsUploadVolumeLimitExceeded { get; set; } = false;
        
        // Số liệu thống kê lưu lượng
        private long _totalDownloadedBytes = 0;
        private long _totalUploadedBytes = 0;
        private readonly object _lockObject = new object();
        
        // Sự kiện được kích hoạt khi có thay đổi lưu lượng đáng kể (mỗi 1MB)
        public event EventHandler<BandwidthEventArgs> BandwidthChanged;
        
        // Sự kiện được kích hoạt khi đạt đến giới hạn dung lượng
        public event EventHandler<VolumeLimitEventArgs> DownloadVolumeLimitReached;
        public event EventHandler<VolumeLimitEventArgs> UploadVolumeLimitReached;
        
        private long _lastNotificationDownload = 0;
        private long _lastNotificationUpload = 0;
        private const long NOTIFICATION_THRESHOLD = 1024 * 1024; // 1MB
        
        /// <summary>
        /// Tổng số byte đã tải xuống
        /// </summary>
        public long TotalDownloadedBytes 
        { 
            get { lock (_lockObject) return _totalDownloadedBytes; } 
        }
        
        /// <summary>
        /// Tổng số byte đã tải lên
        /// </summary>
        public long TotalUploadedBytes 
        { 
            get { lock (_lockObject) return _totalUploadedBytes; } 
        }
        
        /// <summary>
        /// Tốc độ tải xuống hiện tại (bytes/giây)
        /// </summary>
        public long CurrentDownloadSpeed { get; private set; } = 0;
        
        /// <summary>
        /// Tốc độ tải lên hiện tại (bytes/giây)
        /// </summary>
        public long CurrentUploadSpeed { get; private set; } = 0;
        
        // Biến để tính toán tốc độ trung bình
        private long _lastSpeedCalculationTime = 0;
        private long _bytesDownloadedSinceLastCalculation = 0;
        private long _bytesUploadedSinceLastCalculation = 0;
        
        public BandwidthManager()
        {
            // Khởi tạo thời gian tính toán tốc độ
            _lastSpeedCalculationTime = Environment.TickCount;
            
            // Bắt đầu thread tính toán tốc độ
            StartSpeedCalculationThread();
        }
        
        /// <summary>
        /// Cập nhật số liệu thống kê băng thông tải xuống
        /// </summary>
        /// <returns>True nếu cho phép tiếp tục tải xuống, False nếu đã vượt quá giới hạn</returns>
        public bool TrackDownloadedBytes(long bytes)
        {
            lock (_lockObject)
            {
                // Nếu đã vượt quá giới hạn, không cho phép tải xuống thêm
                if (IsDownloadVolumeLimitExceeded)
                    return false;
                
                // Kiểm tra xem liệu lần cộng này có làm vượt quá giới hạn không
                if (DownloadVolumeLimit > 0 && _totalDownloadedBytes + bytes > DownloadVolumeLimit)
                {
                    // Cập nhật lần cuối để đạt đến giới hạn chính xác
                    _totalDownloadedBytes = DownloadVolumeLimit;
                    _bytesDownloadedSinceLastCalculation += (DownloadVolumeLimit - _totalDownloadedBytes);
                    
                    IsDownloadVolumeLimitExceeded = true;
                    OnDownloadVolumeLimitReached();
                    
                    // Kích hoạt sự kiện thông báo
                    OnBandwidthChanged();
                    
                    return false;
                }
                
                _totalDownloadedBytes += bytes;
                _bytesDownloadedSinceLastCalculation += bytes;
                
                // Kiểm tra xem có nên kích hoạt sự kiện hay không
                if (_totalDownloadedBytes - _lastNotificationDownload >= NOTIFICATION_THRESHOLD)
                {
                    _lastNotificationDownload = _totalDownloadedBytes;
                    OnBandwidthChanged();
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Cập nhật số liệu thống kê băng thông tải lên
        /// </summary>
        /// <returns>True nếu cho phép tiếp tục tải lên, False nếu đã vượt quá giới hạn</returns>
        public bool TrackUploadedBytes(long bytes)
        {
            lock (_lockObject)
            {
                // Nếu đã vượt quá giới hạn, không cho phép tải lên thêm
                if (IsUploadVolumeLimitExceeded)
                    return false;
                
                // Kiểm tra xem liệu lần cộng này có làm vượt quá giới hạn không
                if (UploadVolumeLimit > 0 && _totalUploadedBytes + bytes > UploadVolumeLimit)
                {
                    // Cập nhật lần cuối để đạt đến giới hạn chính xác
                    _totalUploadedBytes = UploadVolumeLimit;
                    _bytesUploadedSinceLastCalculation += (UploadVolumeLimit - _totalUploadedBytes);
                    
                    IsUploadVolumeLimitExceeded = true;
                    OnUploadVolumeLimitReached();
                    
                    // Kích hoạt sự kiện thông báo
                    OnBandwidthChanged();
                    
                    return false;
                }
                
                _totalUploadedBytes += bytes;
                _bytesUploadedSinceLastCalculation += bytes;
                
                // Kiểm tra xem có nên kích hoạt sự kiện hay không
                if (_totalUploadedBytes - _lastNotificationUpload >= NOTIFICATION_THRESHOLD)
                {
                    _lastNotificationUpload = _totalUploadedBytes;
                    OnBandwidthChanged();
                }
                
                return true;
            }
        }
        
        /// <summary>
        /// Xóa tất cả số liệu thống kê băng thông và đặt lại cờ giới hạn
        /// </summary>
        public void ResetStats()
        {
            lock (_lockObject)
            {
                _totalDownloadedBytes = 0;
                _totalUploadedBytes = 0;
                _lastNotificationDownload = 0;
                _lastNotificationUpload = 0;
                _bytesDownloadedSinceLastCalculation = 0;
                _bytesUploadedSinceLastCalculation = 0;
                CurrentDownloadSpeed = 0;
                CurrentUploadSpeed = 0;
                IsDownloadVolumeLimitExceeded = false;
                IsUploadVolumeLimitExceeded = false;
                
                OnBandwidthChanged();
            }
        }
        
        /// <summary>
        /// Đặt lại cờ giới hạn dung lượng mà không đặt lại số liệu thống kê
        /// </summary>
        public void ResetVolumeLimitFlags()
        {
            lock (_lockObject)
            {
                IsDownloadVolumeLimitExceeded = false;
                IsUploadVolumeLimitExceeded = false;
            }
        }
        
        /// <summary>
        /// Kích hoạt sự kiện BandwidthChanged
        /// </summary>
        protected virtual void OnBandwidthChanged()
        {
            BandwidthChanged?.Invoke(this, new BandwidthEventArgs
            {
                TotalDownloadedBytes = _totalDownloadedBytes,
                TotalUploadedBytes = _totalUploadedBytes,
                CurrentDownloadSpeed = CurrentDownloadSpeed,
                CurrentUploadSpeed = CurrentUploadSpeed,
                IsDownloadVolumeLimitExceeded = IsDownloadVolumeLimitExceeded,
                IsUploadVolumeLimitExceeded = IsUploadVolumeLimitExceeded
            });
        }
        
        /// <summary>
        /// Kích hoạt sự kiện DownloadVolumeLimitReached
        /// </summary>
        protected virtual void OnDownloadVolumeLimitReached()
        {
            DownloadVolumeLimitReached?.Invoke(this, new VolumeLimitEventArgs
            {
                LimitBytes = DownloadVolumeLimit,
                CurrentBytes = _totalDownloadedBytes
            });
        }
        
        /// <summary>
        /// Kích hoạt sự kiện UploadVolumeLimitReached
        /// </summary>
        protected virtual void OnUploadVolumeLimitReached()
        {
            UploadVolumeLimitReached?.Invoke(this, new VolumeLimitEventArgs
            {
                LimitBytes = UploadVolumeLimit,
                CurrentBytes = _totalUploadedBytes
            });
        }
        
        /// <summary>
        /// Bắt đầu một thread để tính toán tốc độ truyền
        /// </summary>
        private void StartSpeedCalculationThread()
        {
            Thread thread = new Thread(() =>
            {
                while (true)
                {
                    // Tính toán tốc độ mỗi 1 giây
                    Thread.Sleep(1000);
                    CalculateCurrentSpeed();
                }
            })
            {
                IsBackground = true,
                Name = "BandwidthManager_SpeedCalculation"
            };
            
            thread.Start();
        }
        
        /// <summary>
        /// Tính toán tốc độ hiện tại
        /// </summary>
        private void CalculateCurrentSpeed()
        {
            long currentTime = Environment.TickCount;
            long timeDiff = currentTime - _lastSpeedCalculationTime;
            
            // Tránh chia cho 0
            if (timeDiff <= 0)
                return;
            
            lock (_lockObject)
            {
                // Tính toán tốc độ (bytes/giây)
                CurrentDownloadSpeed = (_bytesDownloadedSinceLastCalculation * 1000) / timeDiff;
                CurrentUploadSpeed = (_bytesUploadedSinceLastCalculation * 1000) / timeDiff;
                
                // Reset các biến
                _bytesDownloadedSinceLastCalculation = 0;
                _bytesUploadedSinceLastCalculation = 0;
                _lastSpeedCalculationTime = currentTime;
                
                // Thông báo thay đổi tốc độ nếu có lưu lượng
                if (CurrentDownloadSpeed > 0 || CurrentUploadSpeed > 0)
                {
                    OnBandwidthChanged();
                }
            }
        }
    }
    
    
    
    
}
