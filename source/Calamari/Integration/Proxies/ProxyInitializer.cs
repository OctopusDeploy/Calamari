#if NET40
#else
using NuGet.Configuration;
#endif
using System;
using System.Net;

namespace Calamari.Integration.Proxies
{
    public class ProxyInitializer
    {
        public static void InitializeDefaultProxy()
        {
            var proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername");
            var proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword");
            var proxyHost = Environment.GetEnvironmentVariable("TentacleProxyHost");
            var proxyPortText = Environment.GetEnvironmentVariable("TentacleProxyPort");
            int proxyPort;
            int.TryParse(proxyPortText, out proxyPort);

            var useSystemProxy = string.IsNullOrWhiteSpace(proxyHost);

            var proxy = useSystemProxy
#if NET40
                ? WebRequest.GetSystemWebProxy()
#else
                ? WebRequest.DefaultWebProxy
#endif
                : new WebProxy(new UriBuilder("http", proxyHost, proxyPort).Uri);

            var useDefaultCredentials = string.IsNullOrWhiteSpace(proxyUsername);

            proxy.Credentials = useDefaultCredentials
                ? useSystemProxy
                    ? CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential()
                : new NetworkCredential(proxyUsername, proxyPassword);

            WebRequest.DefaultWebProxy = proxy;
        }
    }
}
