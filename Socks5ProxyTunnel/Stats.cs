﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Socks5ProxyTunnel
{
    public enum PacketType
    {
        Sent,
        Received
    }

    public enum ByteType
    {
        Sent,
        Received
    }

    public class Stats
    {
        public int TotalClients { get; private set; }
        public int ClientsSinceRun { get; private set; }

        public ulong NetworkReceived { get; private set; }
        public ulong NetworkSent { get; private set; }

        public ulong PacketsSent { get; private set; }
        public ulong PacketsReceived { get; private set; }

        private ulong BytesReceivedPerSecond { get; set; }
        private ulong BytesSentPerSecond { get; set; }

        private DateTime _receivedLastRead = DateTime.Now;
        private DateTime _sentLastRead = DateTime.Now;

        public string ReceivedBytesPerSecond()
        {
            var len = BytesReceivedPerSecond / (DateTime.Now - _receivedLastRead).TotalSeconds;
            BytesReceivedPerSecond = 0;
            _receivedLastRead = DateTime.Now;
            return HumanReadable((ulong)len);
        }

        public ulong ReceivedBytesPerSecondNumber()
        {
            var len = BytesReceivedPerSecond / (DateTime.Now - _receivedLastRead).TotalSeconds;
            BytesReceivedPerSecond = 0;
            _receivedLastRead = DateTime.Now;
            return (ulong)len;
        }

        public string SentBytesPerSecond()
        {
            var len = BytesSentPerSecond / (DateTime.Now - _sentLastRead).TotalSeconds;
            BytesSentPerSecond = 0;
            _sentLastRead = DateTime.Now;
            return HumanReadable((ulong)len);
        }

        public string HumanReadable(ulong i)
        {
            // Determine the suffix and readable value
            string suffix;
            double readable;
            if (i >= 0x1000000000000000) // Exabyte
            {
                suffix = "EB";
                readable = (i >> 50);
            }
            else if (i >= 0x4000000000000) // Petabyte
            {
                suffix = "PB";
                readable = (i >> 40);
            }
            else if (i >= 0x10000000000) // Terabyte
            {
                suffix = "TB";
                readable = (i >> 30);
            }
            else if (i >= 0x40000000) // Gigabyte
            {
                suffix = "GB";
                readable = (i >> 20);
            }
            else if (i >= 0x100000) // Megabyte
            {
                suffix = "MB";
                readable = (i >> 10);
            }
            else if (i >= 0x400) // Kilobyte
            {
                suffix = "KB";
                readable = i;
            }
            else
            {
                return i.ToString("0 B"); // Byte
            }
            // Divide by 1024 to get fractional value
            readable = (readable / 1024);
            // Return formatted number with suffix
            return readable.ToString("0.### ") + suffix + "/s";
        }

        public void AddClient()
        {
            TotalClients++;
            ClientsSinceRun++;
        }

        public void ResetClients(int count)
        {
            TotalClients = count;
        }

        public void AddBytes(int bytes, ByteType typ)
        {
            if (typ != ByteType.Sent)
            {
                BytesReceivedPerSecond += (ulong)bytes;
                NetworkReceived += (ulong)bytes;
                return;
            }

            BytesSentPerSecond += (ulong)bytes;
            NetworkSent += (ulong)bytes;
        }

        public void AddPacket(PacketType pkt)
        {
            if (pkt != PacketType.Sent)
            {
                PacketsReceived++;
            }
            else
            {
                PacketsSent++;
            }
        }
    }

}
