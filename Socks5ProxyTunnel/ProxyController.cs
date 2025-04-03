using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Bandwidth;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Exceptions;
using Titanium.Web.Proxy.Extension;
using Titanium.Web.Proxy.Helpers;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy.StreamExtended.Network;

namespace Socks5ProxyTunnel
{
    public class ProxyController : IDisposable
    {
        //private readonly ProxyServer proxyServer;
        private readonly BandwidthEnabledProxyServer proxyServer;
        private ExplicitProxyEndPoint explicitEndPoint;

        private CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private CancellationToken cancellationToken => cancellationTokenSource.Token;
        private ConcurrentQueue<Tuple<ConsoleColor?, string>> consoleMessageQueue
            = new ConcurrentQueue<Tuple<ConsoleColor?, string>>();
        public ProxyOptions _ProxyOptions { get; set; }

        public static LogWriter LogWriter = new LogWriter
        {
            _EnableLog = true
        };
        
        public bool _IsLogin { get; set; }

        public ProxyController()
        {
            
            Task.Run(() => listenToConsole());

            //proxyServer = new ProxyServer();
            proxyServer = new BandwidthEnabledProxyServer();

            //proxyServer.EnableHttp2 = true;

            proxyServer.ProxyAuthenticationRealm = "Proxy Server Auth require?";
            
            // Thiết lập chính sách kiểm soát băng thông
            proxyServer.SetDownloadSpeedLimit(1024 * 1024); // 1 MB/s
            proxyServer.SetUploadSpeedLimit(512 * 1024);   // 512 KB/s
            
            // Đăng ký event để theo dõi băng thông
            proxyServer.BandwidthManager.BandwidthChanged += OnBandwidthChanged;
            
            proxyServer.ExceptionFunc = async exception =>
            {
                if (exception is ProxyHttpException phex)
                {
                    writeToConsole(exception.Message + ": " + phex.InnerException?.Message, ConsoleColor.Red);
                }
                else
                {
                    writeToConsole(exception.Message, ConsoleColor.Red);
                }
            };

            proxyServer.TcpTimeWaitSeconds = 10;
            proxyServer.ConnectionTimeOutSeconds = 15;
            proxyServer.ReuseSocket = false;
            proxyServer.EnableConnectionPool = false;
            proxyServer.ForwardToUpstreamGateway = true;
            proxyServer.CertificateManager.SaveFakeCertificates = false;
            //proxyServer.ProxyBasicAuthenticateFunc = async (args, userName, password) =>
            //{
            //    return true;
            //};

            proxyServer.ProxyBasicAuthenticateFunc = async (args, userName, password) =>
            {
                
                if (userName == _ProxyOptions.proxy_username && password == _ProxyOptions.proxy_password)
                {
                    if (!_IsLogin)
                    {
                        _IsLogin = true;
                    }

                    return true;
                }
                else {
                    return false;
                }
                
            };
            
        }

        public void StartProxy()
        {
            //LogWriter._EnableLog = _ProxyOptions.EnableLog;
            proxyServer.BeforeRequest += onRequest;
            proxyServer.BeforeResponse += onResponse;
            proxyServer.AfterResponse += onAfterResponse;

            proxyServer.ServerCertificateValidationCallback += OnCertificateValidation;
            proxyServer.ClientCertificateSelectionCallback += OnCertificateSelection;

            //proxyServer.EnableWinAuth = true;

            explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Parse(_ProxyOptions.proxy_ipaddress), _ProxyOptions.proxy_listen_port, false);

            // Fired when a CONNECT request is received
            explicitEndPoint.BeforeTunnelConnectRequest += onBeforeTunnelConnectRequest;
            explicitEndPoint.BeforeTunnelConnectResponse += onBeforeTunnelConnectResponse;

            // An explicit endpoint is where the client knows about the existence of a proxy
            // So client sends request in a proxy friendly manner
            proxyServer.AddEndPoint(explicitEndPoint);
            proxyServer.Start();

            // SOCKS proxy
            //proxyServer.UpStreamHttpProxy = new ExternalProxy("127.0.0.1", 1080)
            //    { ProxyType = ExternalProxyType.Socks5, UserName = "User1", Password = "Pass" };
            //proxyServer.UpStreamHttpsProxy = new ExternalProxy("127.0.0.1", 1080)
            //    { ProxyType = ExternalProxyType.Socks5, UserName = "User1", Password = "Pass" };
            
            
            proxyServer.UpStreamHttpProxy = new ExternalProxy(_ProxyOptions.socks5_ipaddress, _ProxyOptions.socks5_port)
                    { ProxyType = ExternalProxyType.Socks5, UserName = _ProxyOptions.socks5_username, Password = _ProxyOptions.sock5_password, BypassLocalhost = false};
            proxyServer.UpStreamHttpsProxy = new ExternalProxy(_ProxyOptions.socks5_ipaddress, _ProxyOptions.socks5_port)
                { ProxyType = ExternalProxyType.Socks5, UserName = _ProxyOptions.socks5_username, Password = _ProxyOptions.sock5_password, BypassLocalhost = false};
            
            /*
            proxyServer.UpStreamHttpProxy = new ExternalProxy("23.105.170.30", 56114)
                { ProxyType = ExternalProxyType.Socks5, BypassLocalhost = false};
            proxyServer.UpStreamHttpsProxy = new ExternalProxy("23.105.170.30", 56114)
                { ProxyType = ExternalProxyType.Socks5, BypassLocalhost = false};
            */
            var socksEndPoint = new SocksProxyEndPoint(IPAddress.Parse(_ProxyOptions.proxy_ipaddress), _ProxyOptions.proxy_socks_listen_port, false);
            proxyServer.AddEndPoint(socksEndPoint);

            foreach (var endPoint in proxyServer.ProxyEndPoints)
            {
                LogWriter._InsLogs("PROXY_CONTROLLER", "INFO", "PROXY_CONTROLLER_StartProxy",
                    $"Listening on '{endPoint.GetType().Name}' endpoint at Ip {endPoint.IpAddress} and port: {endPoint.Port}");
            }
            
        }

        public void Stop()
        {
            explicitEndPoint.BeforeTunnelConnectRequest -= onBeforeTunnelConnectRequest;
            explicitEndPoint.BeforeTunnelConnectResponse -= onBeforeTunnelConnectResponse;

            proxyServer.BeforeRequest -= onRequest;
            proxyServer.BeforeResponse -= onResponse;
            proxyServer.ServerCertificateValidationCallback -= OnCertificateValidation;
            proxyServer.ClientCertificateSelectionCallback -= OnCertificateSelection;

            proxyServer.Stop();

        }

        private async Task<IExternalProxy> onGetCustomUpStreamProxyFunc(SessionEventArgsBase arg)
        {
            arg.GetState().PipelineInfo.AppendLine(nameof(onGetCustomUpStreamProxyFunc));

            // this is just to show the functionality, provided values are junk
            return new ExternalProxy
            {
                BypassLocalhost = false,
                HostName = "127.0.0.9",
                Port = 9090,
                Password = "fake",
                UserName = "fake",
                UseDefaultCredentials = false
            };
        }

        private async Task<IExternalProxy> onCustomUpStreamProxyFailureFunc(SessionEventArgsBase arg)
        {
            arg.GetState().PipelineInfo.AppendLine(nameof(onCustomUpStreamProxyFailureFunc));

            // this is just to show the functionality, provided values are junk
            return new ExternalProxy
            {
                BypassLocalhost = false,
                HostName = "127.0.0.10",
                Port = 9191,
                Password = "fake2",
                UserName = "fake2",
                UseDefaultCredentials = false
            };
        }

        private async Task onBeforeTunnelConnectRequest(object sender, TunnelConnectSessionEventArgs e)
        {
            string hostname = e.HttpClient.Request.RequestUri.Host;
            e.GetState().PipelineInfo.AppendLine(nameof(onBeforeTunnelConnectRequest) + ":" + hostname);
            writeToConsole("Tunnel to: " + hostname);

            var clientLocalIp = e.ClientLocalEndPoint.Address;
            if (!clientLocalIp.Equals(IPAddress.Loopback) && !clientLocalIp.Equals(IPAddress.IPv6Loopback))
            {
                e.HttpClient.UpStreamEndPoint = new IPEndPoint(clientLocalIp, 0);
            }
            
            e.DecryptSsl = false;

        }

        private void WebSocket_DataSent(object sender, DataEventArgs e)
        {
            var args = (SessionEventArgs)sender;
            WebSocketDataSentReceived(args, e, true);
        }

        private void WebSocket_DataReceived(object sender, DataEventArgs e)
        {
            var args = (SessionEventArgs)sender;
            WebSocketDataSentReceived(args, e, false);
        }

        private void WebSocketDataSentReceived(SessionEventArgs args, DataEventArgs e, bool sent)
        {
            var color = sent ? ConsoleColor.Green : ConsoleColor.Blue;

            foreach (var frame in args.WebSocketDecoder.Decode(e.Buffer, e.Offset, e.Count))
            {
                if (frame.OpCode == WebsocketOpCode.Binary)
                {
                    var data = frame.Data.ToArray();
                    string str = string.Join(",", data.ToArray().Select(x => x.ToString("X2")));
                    writeToConsole(str, color);
                }

                if (frame.OpCode == WebsocketOpCode.Text)
                {
                    writeToConsole(frame.GetText(), color);
                }
            }
        }

        private Task onBeforeTunnelConnectResponse(object sender, TunnelConnectSessionEventArgs e)
        {
            e.GetState().PipelineInfo.AppendLine(nameof(onBeforeTunnelConnectResponse) + ":" + e.HttpClient.Request.RequestUri);

            return Task.CompletedTask;
        }

        // intercept & cancel redirect or update requests
        private async Task onRequest(object sender, SessionEventArgs e)
        {

            e.GetState().PipelineInfo.AppendLine(nameof(onRequest) + ":" + e.HttpClient.Request.RequestUri);

            var clientLocalIp = e.ClientLocalEndPoint.Address;
            if (!clientLocalIp.Equals(IPAddress.Loopback) && !clientLocalIp.Equals(IPAddress.IPv6Loopback))
            {
                e.HttpClient.UpStreamEndPoint = new IPEndPoint(clientLocalIp, 0);
            }
            /*
            if (e.HttpClient.Request.Url.Contains("yahoo.com"))
            {
                e.CustomUpStreamProxy = new ExternalProxy("localhost", 8888);
            }
            */
            writeToConsole("Active Client Connections:" + ((ProxyServer)sender).ClientConnectionCount);
            writeToConsole(e.HttpClient.Request.Url);

            // store it in the UserData property
            // It can be a simple integer, Guid, or any type
            //e.UserData = new CustomUserData()
            //{
            //    RequestHeaders = e.HttpClient.Request.Headers,
            //    RequestBody = e.HttpClient.Request.HasBody ? e.HttpClient.Request.Body:null,
            //    RequestBodyString = e.HttpClient.Request.HasBody? e.HttpClient.Request.BodyString:null
            //};

            ////This sample shows how to get the multipart form data headers
            //if (e.HttpClient.Request.Host == "mail.yahoo.com" && e.HttpClient.Request.IsMultipartFormData)
            //{
            //    e.MultipartRequestPartSent += MultipartRequestPartSent;
            //}

            // To cancel a request with a custom HTML content
            // Filter URL
            //if (e.HttpClient.Request.RequestUri.AbsoluteUri.Contains("yahoo.com"))
            //{ 
            //    e.Ok("<!DOCTYPE html>" +
            //          "<html><body><h1>" +
            //          "Website Blocked" +
            //          "</h1>" +
            //          "<p>Blocked by titanium web proxy.</p>" +
            //          "</body>" +
            //          "</html>");
            //} 

            ////Redirect example
            //if (e.HttpClient.Request.RequestUri.AbsoluteUri.Contains("wikipedia.org"))
            //{ 
            //   e.Redirect("https://www.paypal.com");
            //} 
        }

        // Modify response
        private async Task multipartRequestPartSent(object sender, MultipartRequestPartSentEventArgs e)
        {
            e.GetState().PipelineInfo.AppendLine(nameof(multipartRequestPartSent));

            var session = (SessionEventArgs)sender;
            writeToConsole("Multipart form data headers:");
            foreach (var header in e.Headers)
            {
                writeToConsole(header.ToString());
            }
        }

        private async Task onResponse(object sender, SessionEventArgs e)
        {
            e.GetState().PipelineInfo.AppendLine(nameof(onResponse));

            if (e.HttpClient.ConnectRequest?.TunnelType == TunnelType.Websocket)
            {
                e.DataSent += WebSocket_DataSent;
                e.DataReceived += WebSocket_DataReceived;
            }

            writeToConsole("Active Server Connections:" + ((ProxyServer)sender).ServerConnectionCount);

            string ext = System.IO.Path.GetExtension(e.HttpClient.Request.RequestUri.AbsolutePath);

            // access user data set in request to do something with it
            //var userData = e.HttpClient.UserData as CustomUserData;

            //if (ext == ".gif" || ext == ".png" || ext == ".jpg")
            //{ 
            //    byte[] btBody = Encoding.UTF8.GetBytes("<!DOCTYPE html>" +
            //                                           "<html><body><h1>" +
            //                                           "Image is blocked" +
            //                                           "</h1>" +
            //                                           "<p>Blocked by Titanium</p>" +
            //                                           "</body>" +
            //                                           "</html>");

            //    var response = new OkResponse(btBody);
            //    response.HttpVersion = e.HttpClient.Request.HttpVersion;

            //    e.Respond(response);
            //    e.TerminateServerConnection();
            //} 

            //// print out process id of current session
            ////WriteToConsole($"PID: {e.HttpClient.ProcessId.Value}");

            ////if (!e.ProxySession.Request.Host.Equals("medeczane.sgk.gov.tr")) return;
            //if (e.HttpClient.Request.Method == "GET" || e.HttpClient.Request.Method == "POST")
            //{
            //    if (e.HttpClient.Response.StatusCode == (int)HttpStatusCode.OK)
            //    {
            //        if (e.HttpClient.Response.ContentType != null && e.HttpClient.Response.ContentType.Trim().ToLower().Contains("text/html"))
            //        {
            //            var bodyBytes = await e.GetResponseBody();
            //            e.SetResponseBody(bodyBytes);

            //            string body = await e.GetResponseBodyAsString();
            //            e.SetResponseBodyString(body);
            //        }
            //    }
            //}
        }

        private async Task onAfterResponse(object sender, SessionEventArgs e)
        {
            writeToConsole($"Pipelineinfo: {e.GetState().PipelineInfo}", ConsoleColor.Yellow);
        }

        /// <summary>
        ///     Allows overriding default certificate validation logic
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public Task OnCertificateValidation(object sender, CertificateValidationEventArgs e)
        {
            e.GetState().PipelineInfo.AppendLine(nameof(OnCertificateValidation));

            // set IsValid to true/false based on Certificate Errors
            if (e.SslPolicyErrors == SslPolicyErrors.None)
            {
                e.IsValid = true;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///     Allows overriding default client certificate selection logic during mutual authentication
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public Task OnCertificateSelection(object sender, CertificateSelectionEventArgs e)
        {
            e.GetState().PipelineInfo.AppendLine(nameof(OnCertificateSelection));

            // set e.clientCertificate to override

            return Task.CompletedTask;
        }

        private void writeToConsole(string message, ConsoleColor? consoleColor = null)
        {
            LogWriter._InsLogs("PROXY_CONTROLLER", consoleColor == ConsoleColor.Red? "ERROR" : "INFO", $"API_SERVER_writeToConsole[{_ProxyOptions.proxy_ipaddress}:{_ProxyOptions.proxy_listen_port}]",
                message);
            //consoleMessageQueue.Enqueue(new Tuple<ConsoleColor?, string>(consoleColor, message));
        }

        private async Task listenToConsole()
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                while (consoleMessageQueue.TryDequeue(out var item))
                {
                    var consoleColor = item.Item1;
                    var message = item.Item2;

                    if (consoleColor.HasValue)
                    {
                        ConsoleColor existing = Console.ForegroundColor;
                        Console.ForegroundColor = consoleColor.Value;
                        Console.WriteLine(message);
                        Console.ForegroundColor = existing;
                    }
                    else
                    {
                        Console.WriteLine(message);
                    }
                }

                //reduce CPU usage
                await Task.Delay(50);
            }
        }

        public void Dispose()
        {
            // Hủy đăng ký sự kiện băng thông
            if (proxyServer?.BandwidthManager != null)
            {
                proxyServer.BandwidthManager.BandwidthChanged -= OnBandwidthChanged;
                proxyServer.BandwidthManager.DownloadVolumeLimitReached -= OnDownloadVolumeLimitReached;
                proxyServer.BandwidthManager.UploadVolumeLimitReached -= OnUploadVolumeLimitReached;
            }
            
            cancellationTokenSource.Dispose();
            proxyServer?.Dispose();
        }

        ///// <summary>
        ///// User data object as defined by user.
        ///// User data can be set to each SessionEventArgs.HttpClient.UserData property
        ///// </summary>
        //public class CustomUserData
        //{
        //    public HeaderCollection RequestHeaders { get; set; }
        //    public byte[] RequestBody { get; set; }
        //    public string RequestBodyString { get; set; }
        //}
        
        /// <summary>
        /// Xử lý sự kiện khi băng thông thay đổi
        /// </summary>
        private void OnBandwidthChanged(object sender, BandwidthEventArgs e)
        {
            Console.WriteLine($"Download: Total {FormatBytes(e.TotalDownloadedBytes)}, Speed {FormatBytes(e.CurrentDownloadSpeed)}/s");
            Console.WriteLine($"Upload: Total {FormatBytes(e.TotalUploadedBytes)}, Speed {FormatBytes(e.CurrentUploadSpeed)}/s");
        }
        
        /// <summary>
        /// Xử lý sự kiện khi đạt đến giới hạn dung lượng tải xuống
        /// </summary>
        private void OnDownloadVolumeLimitReached(object sender, VolumeLimitEventArgs e)
        {
            Console.WriteLine($"WARNING: Download volume limit of {FormatBytes(e.LimitBytes)} has been reached!");
            Console.WriteLine("Further download requests will be blocked until the limit is reset.");
            
            // Có thể thêm hành động như gửi thông báo, ghi log, v.v.
        }
        
        /// <summary>
        /// Xử lý sự kiện khi đạt đến giới hạn dung lượng tải lên
        /// </summary>
        private void OnUploadVolumeLimitReached(object sender, VolumeLimitEventArgs e)
        {
            Console.WriteLine($"WARNING: Upload volume limit of {FormatBytes(e.LimitBytes)} has been reached!");
            Console.WriteLine("Further upload requests will be blocked until the limit is reset.");
            
            // Có thể thêm hành động như gửi thông báo, ghi log, v.v.
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
        /// Các phương thức xử lý sự kiện khác giữ nguyên...
        /// </summary>
        
        // Các phương thức điều khiển băng thông
        
        /// <summary>
        /// Thiết lập giới hạn tốc độ tải xuống
        /// </summary>
        public void SetDownloadLimit(long bytesPerSecond)
        {
            proxyServer.SetDownloadSpeedLimit(bytesPerSecond);
            Console.WriteLine($"Download speed limit set to {FormatBytes(bytesPerSecond)}/s");
        }
        
        /// <summary>
        /// Thiết lập giới hạn tốc độ tải lên
        /// </summary>
        public void SetUploadLimit(long bytesPerSecond)
        {
            proxyServer.SetUploadSpeedLimit(bytesPerSecond);
            Console.WriteLine($"Upload speed limit set to {FormatBytes(bytesPerSecond)}/s");
        }
        
        /// <summary>
        /// Vô hiệu hóa tất cả giới hạn
        /// </summary>
        public void DisableAllLimits()
        {
            proxyServer.SetDownloadSpeedLimit(-1);
            proxyServer.SetUploadSpeedLimit(-1);
            proxyServer.SetDownloadVolumeLimit(-1);
            proxyServer.SetUploadVolumeLimit(-1);
            proxyServer.ResetVolumeLimits();
            Console.WriteLine("All bandwidth limits have been disabled.");
        }
        
        /// <summary>
        /// Thiết lập giới hạn dung lượng tải xuống
        /// </summary>
        public void SetDownloadVolumeLimit(long bytes)
        {
            proxyServer.SetDownloadVolumeLimit(bytes);
            Console.WriteLine($"Download volume limit set to {FormatBytes(bytes)}");
        }
        
        /// <summary>
        /// Đặt lại cờ giới hạn dung lượng (cho phép tải lên/xuống tiếp tục)
        /// </summary>
        public void ResetVolumeLimits()
        {
            proxyServer.ResetVolumeLimits();
            Console.WriteLine("Volume limit flags have been reset. Data transfer is now allowed.");
        }
        
        /// <summary>
        /// Thiết lập giới hạn dung lượng tải lên
        /// </summary>
        public void SetUploadVolumeLimit(long bytes)
        {
            proxyServer.SetUploadVolumeLimit(bytes);
            Console.WriteLine($"Upload volume limit set to {FormatBytes(bytes)}");
        }
        
        /// <summary>
        /// Hiển thị thống kê băng thông hiện tại
        /// </summary>
        public void ShowBandwidthStats()
        {
            long downloadedBytes = proxyServer.GetTotalDownloadedBytes();
            long uploadedBytes = proxyServer.GetTotalUploadedBytes();
            
            Console.WriteLine("\nBandwidth Statistics:");
            Console.WriteLine($"Total Downloaded: {FormatBytes(downloadedBytes)}");
            Console.WriteLine($"Total Uploaded: {FormatBytes(uploadedBytes)}");
            Console.WriteLine($"Download Speed: {FormatBytes(proxyServer.BandwidthManager.CurrentDownloadSpeed)}/s");
            Console.WriteLine($"Upload Speed: {FormatBytes(proxyServer.BandwidthManager.CurrentUploadSpeed)}/s");
            
            Console.WriteLine("\nBandwidth Limits:");
            Console.WriteLine($"Download Speed Limit: {FormatBytes(proxyServer.BandwidthManager.DownloadSpeedLimit)}/s");
            Console.WriteLine($"Upload Speed Limit: {FormatBytes(proxyServer.BandwidthManager.UploadSpeedLimit)}/s");
            Console.WriteLine($"Download Volume Limit: {FormatBytes(proxyServer.BandwidthManager.DownloadVolumeLimit)}");
            Console.WriteLine($"Upload Volume Limit: {FormatBytes(proxyServer.BandwidthManager.UploadVolumeLimit)}");
            
            Console.WriteLine("\nVolume Limit Status:");
            Console.WriteLine($"Download Volume Limit Exceeded: {proxyServer.BandwidthManager.IsDownloadVolumeLimitExceeded}");
            Console.WriteLine($"Upload Volume Limit Exceeded: {proxyServer.BandwidthManager.IsUploadVolumeLimitExceeded}");
        }
        
        /// <summary>
        /// Đặt lại thống kê băng thông
        /// </summary>
        public void ResetBandwidthStats()
        {
            proxyServer.ResetBandwidthStats();
            Console.WriteLine("Bandwidth statistics reset");
        }
    }
}
