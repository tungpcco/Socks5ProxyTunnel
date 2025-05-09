﻿using Titanium.Web.Proxy.EventArguments;

namespace Socks5ProxyTunnel
{
    public static class ProxyEventArgsBaseExtensions
    {
        public static SampleClientState GetState(this ProxyEventArgsBase args)
        {
            if (args.ClientUserData == null)
            {
                args.ClientUserData = new SampleClientState();
            }

            return (SampleClientState)args.ClientUserData;
        }
    }
}
