namespace Socks5ProxyTunnel;

public class ProxyOptions
{
    public string socks5_ipaddress { get; set; }
    public int socks5_port { get; set; }
    public string socks5_username { get; set; }
    public string sock5_password { get; set; }

    public string proxy_ipaddress { get; set; }
    public int proxy_listen_port { get; set; }
    public int proxy_socks_listen_port { get; set; }
    public string proxy_username { get; set; }
    public string proxy_password { get; set; }
    public bool EnableLog { get; set; }
}