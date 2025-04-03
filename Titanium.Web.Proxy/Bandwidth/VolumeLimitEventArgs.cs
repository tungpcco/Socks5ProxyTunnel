using System;

namespace Titanium.Web.Proxy.Bandwidth
{
    /// <summary>
    /// Event args cho sự kiện đạt đến giới hạn dung lượng
    /// </summary>
    public class VolumeLimitEventArgs : EventArgs
    {
        public long LimitBytes { get; set; }
        public long CurrentBytes { get; set; }
    }
}
