using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;

namespace Socks5ProxyTunnel
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
            ProxyOptions optionsX = new ProxyOptions
            {
                socks5_ipaddress = "127.0.0.1",
                socks5_port = 8886,
                socks5_username = "admin",
                sock5_password = "admin123",
                proxy_ipaddress = "0.0.0.0",
                proxy_listen_port = 8089,
                proxy_socks_listen_port = 8088,
                proxy_username = "admin",
                proxy_password = "admin123"
                
            };
            
            File.WriteAllText("config.cfn", JsonConvert.SerializeObject(optionsX));
            */
            if (args.Count() <= 0)
            {
                Console.Write("Invalid Parameter!");
                Environment.Exit(0);
            }

            string _cfnPath = File.Exists(args[0]) ? args[0] : "";
            if (string.IsNullOrEmpty(_cfnPath))
            {
                Console.Write("Invalid Config path!");
                Environment.Exit(0);
            }
            ProxyOptions options = JsonConvert.DeserializeObject<ProxyOptions>(File.ReadAllText(_cfnPath));
            

            ProxyController _server = new ProxyController();
            _server._ProxyOptions = options;
            _server.StartProxy();
            while (true)
            {
                int cBin = (int)(60000 - ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) % 60000));
                Thread.Sleep(cBin);
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        
        public int GetNextFreePort(string _IPAddress = "")
        {
            var listener = new TcpListener( string.IsNullOrEmpty(_IPAddress) ? IPAddress.Loopback : IPAddress.Parse(_IPAddress), 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();

            return port;
        }
        
    }
}