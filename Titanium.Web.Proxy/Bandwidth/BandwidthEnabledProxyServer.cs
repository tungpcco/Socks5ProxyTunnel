using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Bandwidth;

namespace Titanium.Web.Proxy.Extension
{
    /// <summary>
    /// Mở rộng ProxyServer với khả năng quản lý băng thông
    /// </summary>
    public class BandwidthEnabledProxyServer : ProxyServer
    {
        /// <summary>
        /// Quản lý băng thông
        /// </summary>
        public BandwidthManager BandwidthManager { get; }
        
        public BandwidthEnabledProxyServer() : base()
        {
            BandwidthManager = new BandwidthManager();
            
            // Đăng ký xử lý yêu cầu và phản hồi
            BeforeRequest += OnBeforeRequest;
            BeforeResponse += OnBeforeResponse;
            
            // Đăng ký các sự kiện giới hạn dung lượng
            BandwidthManager.DownloadVolumeLimitReached += OnDownloadVolumeLimitReached;
            BandwidthManager.UploadVolumeLimitReached += OnUploadVolumeLimitReached;
        }
        
        private void OnDownloadVolumeLimitReached(object sender, VolumeLimitEventArgs e)
        {
            Console.WriteLine($"Download volume limit of {FormatBytes(e.LimitBytes)} has been reached.");
            // Bạn có thể thêm hành động thêm khi giới hạn tải xuống bị vượt quá
        }
        
        private void OnUploadVolumeLimitReached(object sender, VolumeLimitEventArgs e)
        {
            Console.WriteLine($"Upload volume limit of {FormatBytes(e.LimitBytes)} has been reached.");
            // Bạn có thể thêm hành động thêm khi giới hạn tải lên bị vượt quá
        }
        
        private async Task OnBeforeRequest(object sender, SessionEventArgs e)
        {
            // Đối với yêu cầu có body, chúng ta cần theo dõi và giới hạn tốc độ tải lên
            if (e.HttpClient.Request.HasBody)
            {
                try
                {
                    // Nếu đã vượt quá giới hạn dung lượng tải lên, hủy yêu cầu
                    if (BandwidthManager.IsUploadVolumeLimitExceeded)
                    {
                        e.Ok(GenerateVolumeLimitExceededResponse("Upload volume limit exceeded"));
                        return;
                    }
                    
                    // Lấy body gốc
                    byte[] originalBody = await e.GetRequestBody();
                    
                    // Ghi nhận lưu lượng tải lên
                    bool allowContinue = BandwidthManager.TrackUploadedBytes(originalBody.Length);
                    
                    if (!allowContinue)
                    {
                        // Nếu vượt quá giới hạn trong lần này, hủy yêu cầu
                        e.Ok(GenerateVolumeLimitExceededResponse("Upload volume limit exceeded"));
                        return;
                    }
                    
                    // Nếu không cần giới hạn tốc độ tải lên, chỉ cần đặt lại body
                    if (BandwidthManager.UploadSpeedLimit <= 0)
                    {
                        e.SetRequestBody(originalBody);
                        return;
                    }
                    
                    // Giả lập throttling bằng cách trì hoãn nếu cần
                    if (BandwidthManager.UploadSpeedLimit > 0)
                    {
                        long delayMs = CalculateThrottleDelay(originalBody.Length, BandwidthManager.UploadSpeedLimit);
                        
                        if (delayMs > 0)
                        {
                            await Task.Delay((int)delayMs);
                        }
                    }
                    
                    // Đặt lại body sau khi đã throttle
                    e.SetRequestBody(originalBody);
                }
                catch (Exception ex)
                {
                    // Log lỗi nếu cần
                    System.Diagnostics.Debug.WriteLine($"Error in request throttling: {ex.Message}");
                }
            }
        }
        
        private async Task OnBeforeResponse(object sender, SessionEventArgs e)
        {
            // Đối với phản hồi có body, chúng ta cần theo dõi và giới hạn tốc độ tải xuống
            if (e.HttpClient.Response.HasBody)
            {
                try
                {
                    // Nếu đã vượt quá giới hạn dung lượng tải xuống, thay thế phản hồi
                    if (BandwidthManager.IsDownloadVolumeLimitExceeded)
                    {
                        e.Ok(GenerateVolumeLimitExceededResponse("Download volume limit exceeded"));
                        return;
                    }
                    
                    // Lấy body gốc
                    byte[] originalBody = await e.GetResponseBody();
                    
                    // Ghi nhận lưu lượng tải xuống
                    bool allowContinue = BandwidthManager.TrackDownloadedBytes(originalBody.Length);
                    
                    if (!allowContinue)
                    {
                        // Nếu vượt quá giới hạn trong lần này, thay thế phản hồi
                        e.Ok(GenerateVolumeLimitExceededResponse("Download volume limit exceeded"));
                        return;
                    }
                    
                    // Nếu không cần giới hạn tốc độ tải xuống, chỉ cần đặt lại body
                    if (BandwidthManager.DownloadSpeedLimit <= 0)
                    {
                        e.SetResponseBody(originalBody);
                        return;
                    }
                    
                    // Giả lập throttling bằng cách trì hoãn nếu cần
                    if (BandwidthManager.DownloadSpeedLimit > 0)
                    {
                        long delayMs = CalculateThrottleDelay(originalBody.Length, BandwidthManager.DownloadSpeedLimit);
                        
                        if (delayMs > 0)
                        {
                            await Task.Delay((int)delayMs);
                        }
                    }
                    
                    // Đặt lại body sau khi đã throttle
                    e.SetResponseBody(originalBody);
                }
                catch (Exception ex)
                {
                    // Log lỗi nếu cần
                    System.Diagnostics.Debug.WriteLine($"Error in response throttling: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Tạo phản hồi HTML thông báo khi vượt quá giới hạn dung lượng
        /// </summary>
        private string GenerateVolumeLimitExceededResponse(string message)
        {
            return $"<!DOCTYPE html>\r\n" +
                   $"<html>\r\n" +
                   $"<head>\r\n" +
                   $"    <title>Bandwidth Limit Exceeded</title>\r\n" +
                   $"    <style>\r\n" +
                   $"        body {{ font-family: Arial, sans-serif; padding: 20px; text-align: center; }}\r\n" +
                   $"        h1 {{ color: #d9534f; }}\r\n" +
                   $"        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }}\r\n" +
                   $"    </style>\r\n" +
                   $"</head>\r\n" +
                   $"<body>\r\n" +
                   $"    <div class=\"container\">\r\n" +
                   $"        <h1>Bandwidth Limit Exceeded</h1>\r\n" +
                   $"        <p>{message}</p>\r\n" +
                   $"        <p>The configured data transfer limit has been reached.</p>\r\n" +
                   $"    </div>\r\n" +
                   $"</body>\r\n" +
                   $"</html>";
        }
        
        /// <summary>
        /// Tính toán thời gian trì hoãn cần thiết để đạt được tốc độ giới hạn
        /// </summary>
        /// <param name="byteCount">Số byte cần truyền</param>
        /// <param name="bytesPerSecond">Giới hạn byte mỗi giây</param>
        /// <returns>Số millisecond cần trì hoãn</returns>
        private long CalculateThrottleDelay(long byteCount, long bytesPerSecond)
        {
            if (bytesPerSecond <= 0 || byteCount <= 0)
                return 0;
            
            // Tính thời gian lý tưởng để truyền số byte này theo tốc độ giới hạn (ms)
            long optimalTransferTimeMs = (byteCount * 1000) / bytesPerSecond;
            
            // Thêm một chút trì hoãn để đảm bảo chúng ta không vượt quá giới hạn
            return optimalTransferTimeMs;
        }
        
        /// <summary>
        /// Format số byte thành định dạng dễ đọc
        /// </summary>
        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1) 
            {
                order++;
                len = len / 1024;
            }
            
            return $"{len:0.##} {sizes[order]}";
        }
        
        /// <summary>
        /// Giải phóng tài nguyên
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BeforeRequest -= OnBeforeRequest;
                BeforeResponse -= OnBeforeResponse;
                
                if (BandwidthManager != null)
                {
                    BandwidthManager.DownloadVolumeLimitReached -= OnDownloadVolumeLimitReached;
                    BandwidthManager.UploadVolumeLimitReached -= OnUploadVolumeLimitReached;
                }
            }
            
            base.Dispose(disposing);
        }
        
        /// <summary>
        /// Thiết lập giới hạn tốc độ tải xuống (bytes/giây)
        /// </summary>
        /// <param name="bytesPerSecond">-1 để vô hiệu hóa giới hạn</param>
        public void SetDownloadSpeedLimit(long bytesPerSecond)
        {
            BandwidthManager.DownloadSpeedLimit = bytesPerSecond;
        }
        
        /// <summary>
        /// Thiết lập giới hạn tốc độ tải lên (bytes/giây)
        /// </summary>
        /// <param name="bytesPerSecond">-1 để vô hiệu hóa giới hạn</param>
        public void SetUploadSpeedLimit(long bytesPerSecond)
        {
            BandwidthManager.UploadSpeedLimit = bytesPerSecond;
        }
        
        /// <summary>
        /// Thiết lập giới hạn dung lượng tải xuống (bytes)
        /// </summary>
        /// <param name="bytes">-1 để vô hiệu hóa giới hạn</param>
        public void SetDownloadVolumeLimit(long bytes)
        {
            BandwidthManager.DownloadVolumeLimit = bytes;
            BandwidthManager.IsDownloadVolumeLimitExceeded = false;
        }
        
        /// <summary>
        /// Thiết lập giới hạn dung lượng tải lên (bytes)
        /// </summary>
        /// <param name="bytes">-1 để vô hiệu hóa giới hạn</param>
        public void SetUploadVolumeLimit(long bytes)
        {
            BandwidthManager.UploadVolumeLimit = bytes;
            BandwidthManager.IsUploadVolumeLimitExceeded = false;
        }
        
        /// <summary>
        /// Đặt lại cờ giới hạn dung lượng
        /// </summary>
        public void ResetVolumeLimits()
        {
            BandwidthManager.ResetVolumeLimitFlags();
        }
        
        /// <summary>
        /// Lấy tổng số byte đã tải xuống
        /// </summary>
        public long GetTotalDownloadedBytes()
        {
            return BandwidthManager.TotalDownloadedBytes;
        }
        
        /// <summary>
        /// Lấy tổng số byte đã tải lên
        /// </summary>
        public long GetTotalUploadedBytes()
        {
            return BandwidthManager.TotalUploadedBytes;
        }
        
        /// <summary>
        /// Đặt lại số liệu theo dõi lưu lượng
        /// </summary>
        public void ResetBandwidthStats()
        {
            BandwidthManager.ResetStats();
        }
    }
}
