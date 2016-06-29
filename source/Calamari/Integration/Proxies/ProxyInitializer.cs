using System;
using System.Net;

namespace Calamari.Integration.Proxies
{
    public class ProxyInitializer
    {
        public static void InitializeDefaultProxy()
        {
            var proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername");
            Log.Info($"TentacleProxyUsername: {proxyUsername}");
            var proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword");
            Log.Info($"TentacleProxyPassword: {proxyPassword}");

            Log.Info($"WebRequest.DefaultWebProxy: {WebRequest.DefaultWebProxy}");
            Log.Info($"Custom Creds: {string.IsNullOrWhiteSpace(proxyUsername)}");
            Log.Info($"CredentialCache.DefaultCredentials: {CredentialCache.DefaultCredentials}");
            Log.Info($"NetworkCredential: {new NetworkCredential(proxyUsername, proxyPassword)}");


            if (string.IsNullOrWhiteSpace(proxyUsername))
            {
                WebRequest.DefaultWebProxy.Credentials = CredentialCache.DefaultCredentials;
            }
            else
            {
                WebRequest.DefaultWebProxy.Credentials = new NetworkCredential(proxyUsername, proxyPassword);
            }
        }
    }
}
