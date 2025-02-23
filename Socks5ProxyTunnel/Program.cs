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
            ProxyOptions options = new ProxyOptions();
            
            if (args.Count() <= 0)
            {
                Console.WriteLine("Invalid Parameter!\r\ns5tunnel <socks5_ipaddress> <socks5_port> <socks5_username> <sock5_password> <proxy_ipaddress> <proxy_listen_port> <proxy_socks_listen_port> <proxy_username> <proxy_password>");
                Environment.Exit(0);
            }else{
                if (File.Exists(args[0]))
                {
                    options = JsonConvert.DeserializeObject<ProxyOptions>(File.ReadAllText(args[0]));
                }else{
                    try
                    {
                        options = new ProxyOptions
                        {
                            socks5_ipaddress = args[0],
                            socks5_port = Convert.ToInt32( args[1]),
                            socks5_username = args[2],
                            sock5_password = args[3],
                            proxy_ipaddress = args[4],
                            proxy_listen_port = Convert.ToInt32( args[5]),
                            proxy_socks_listen_port = Convert.ToInt32(args[6]),
                            proxy_username = args[7],
                            proxy_password = args[8],
                            EnableLog = false
                        };
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Invalid Parameter!\r\ns5tunnel <socks5_ipaddress> <socks5_port> <socks5_username> <sock5_password> <proxy_ipaddress> <proxy_listen_port> <proxy_socks_listen_port> <proxy_username> <proxy_password>");
                        Environment.Exit(0);
                    }
                }
            }

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