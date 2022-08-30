using System;
using System.Net;
using Octopus.CoreUtilities;

namespace Calamari.Common.Plumbing.Proxies
{
    public static class ProxyInitializer
    {
        public static void InitializeDefaultProxy()
        {
            var proxy = ProxySettingsInitializer.GetProxySettingsFromEnvironment().CreateProxy();

            if (proxy.Some())
                WebRequest.DefaultWebProxy = proxy.Value;
        }
    }
}