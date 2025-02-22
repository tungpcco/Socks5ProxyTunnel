Socks5 Proxy System Need to install dotnet sdk 9.0 to run the program.

config file structure:

{ "socks5_ipaddress": "192.168.1.23", "socks5_port": 8886, "socks5_username": "admin", "sock5_password": "admin123", "proxy_ipaddress": "0.0.0.0", "proxy_listen_port": 8089, "proxy_socks_listen_port": 8088, "proxy_username": "admin", "proxy_password": "admin123", "EnableLog": false }

run command: after downloading run setup.sh Program run command: s5tunnel [path to configuration file]
