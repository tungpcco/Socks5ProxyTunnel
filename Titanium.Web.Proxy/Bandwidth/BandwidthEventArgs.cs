using System;

namespace Titanium.Web.Proxy.Bandwidth
{
    /// <summary>
    /// Event args cho sự kiện thay đổi băng thông
    /// </summary>
    public class BandwidthEventArgs : EventArgs
    {
        public long TotalDownloadedBytes { get; set; }
        public long TotalUploadedBytes { get; set; }
        public long CurrentDownloadSpeed { get; set; }
        public long CurrentUploadSpeed { get; set; }
        public bool IsDownloadVolumeLimitExceeded { get; set; }
        public bool IsUploadVolumeLimitExceeded { get; set; }
    }
}
