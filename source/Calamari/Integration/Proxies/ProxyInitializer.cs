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

            WebRequest.DefaultWebProxy.Credentials = string.IsNullOrWhiteSpace(proxyUsername) 
                ? CredentialCache.DefaultCredentials 
                : new NetworkCredential(proxyUsername, proxyPassword);
        }
    }
}
