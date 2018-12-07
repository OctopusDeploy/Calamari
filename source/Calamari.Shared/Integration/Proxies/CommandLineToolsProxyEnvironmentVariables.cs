using System;
using System.Collections.Specialized;
using System.Net;
using System.Net.Sockets;

namespace Calamari.Integration.Proxies
{
    public class CommandLineToolsProxyEnvironmentVariables
    {
        public CommandLineToolsProxyEnvironmentVariables()
        {
            var proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername");
            var proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword");
            var proxyHost = Environment.GetEnvironmentVariable("TentacleProxyHost");
            var proxyPortText = Environment.GetEnvironmentVariable("TentacleProxyPort");
            int.TryParse(proxyPortText, out var proxyPort);

            var useSystemProxy = string.IsNullOrWhiteSpace(proxyHost);

            if (useSystemProxy)
            {
                SetProxyEnvironmentVariablesFromSystemProxy(proxyUsername, proxyPassword);
            }
            else
            {
                SetProxyEnvironmentVariables(proxyHost,  proxyPort, proxyUsername, proxyPassword);
            }
        }

        public StringDictionary EnvironmentVariables { get; } = new StringDictionary();

        void SetProxyEnvironmentVariablesFromSystemProxy(string proxyUsername, string proxyPassword)
        {
            #if !NETSTANDARD2_0
            try
            {
                var testUri = WebRequest.DefaultWebProxy.GetProxy(new Uri("https://octopus.com"));
                if (testUri.Host != "octopus.com")
                {
                    SetProxyEnvironmentVariables(testUri.Host, testUri.Port, proxyUsername, proxyPassword);
                }
            }
            catch (SocketException)
            {
                Log.Error("Failed to get the system proxy settings. Calamari will not use any proxy settings.");
            }
            #endif
        }

        void SetProxyEnvironmentVariables(string host, int port, string proxyUsername, string proxyPassword)
        {
            var proxyUri = new Uri($"http://{host}:{port}");
            if (!string.IsNullOrEmpty(proxyUsername))
            {
#if NET40
                proxyUri = new Uri(
                    $"http://{System.Web.HttpUtility.UrlEncode(proxyUsername)}:{System.Web.HttpUtility.UrlEncode(proxyPassword)}@{proxyUri}");
#else
                proxyUri =
                    new Uri($"http://{WebUtility.UrlEncode(proxyUsername)}:{WebUtility.UrlEncode(proxyPassword)}@{host}:{port}");
#endif
            }
	
            if(String.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTP_PROXY")))
            {
                EnvironmentVariables.Add("HTTP_PROXY", proxyUri.ToString());
            }
	
            if(String.IsNullOrEmpty(Environment.GetEnvironmentVariable("HTTPS_PROXY")))
            {
                EnvironmentVariables.Add("HTTPS_PROXY", proxyUri.ToString());
            }
	
            if(String.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_PROXY"))) 
            {
                EnvironmentVariables.Add("NO_PROXY", "127.0.0.1,localhost,169.254.169.254");
            }
        }
    }
}