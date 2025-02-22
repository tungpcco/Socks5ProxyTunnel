#!/bin/bash
mkdir /opt/Socks5ProxyTunnel/
cp -r * /opt/Socks5ProxyTunnel/
chmod +x /opt/Socks5ProxyTunnel/run.sh
sudo ln -sv /opt/Socks5ProxyTunnel/run.sh /usr/bin/s5tunnel
echo "Install successed -> run s5tunnel"