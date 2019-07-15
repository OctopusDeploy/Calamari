using System;
using System.Net;
using System.Net.Sockets;

namespace Calamari.Integration.Proxies
{
    public static class ProxyInitializer
    {
        public static void InitializeDefaultProxy()
        {
            try
            {
                bool useDefaultProxy;
                if (!Boolean.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy),
                    out useDefaultProxy))
                    useDefaultProxy = true;

                var proxyUsername = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername);
                var proxyPassword = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword);
                var proxyHost = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost);
                var proxyPortText = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort);
                int.TryParse(proxyPortText, out var proxyPort);

                var useCustomProxy = !string.IsNullOrWhiteSpace(proxyHost);
                var proxy = useCustomProxy
                    ? new WebProxy(new UriBuilder("http", proxyHost, proxyPort).Uri)
                    : useDefaultProxy
                        ? WebRequest.GetSystemWebProxy()
                        : new WebProxy();

                var useDefaultCredentials = string.IsNullOrWhiteSpace(proxyUsername);

                proxy.Credentials = useDefaultCredentials
                    ? useCustomProxy
                        ? new NetworkCredential()
                        : CredentialCache.DefaultNetworkCredentials
                    : new NetworkCredential(proxyUsername, proxyPassword);

                Log.Verbose(useCustomProxy
                    ? $"Proxy mode: Custom Proxy ({proxyHost}:{proxyPort})"
                    : useDefaultProxy
                        ? "Proxy mode: Default proxy"
                        : "Proxy mode: No Proxy");

                WebRequest.DefaultWebProxy = proxy;
            }
            catch (SocketException)
            {
                /*
                 Ignore this exception. It is probably just an inability to get the IE proxy settings. e.g.
                 
                 Unhandled Exception: System.Net.Sockets.SocketException: The requested service provider could not be loaded or initialized
                   at System.Net.SafeCloseSocketAndEvent.CreateWSASocketWithEvent(AddressFamily addressFamily, SocketType socketType, ProtocolType protocolType, Boolean autoReset, Boolean signaled)
                   at System.Net.NetworkAddressChangePolled..ctor()
                   at System.Net.AutoWebProxyScriptEngine.AutoDetector.Initialize()
                   at System.Net.AutoWebProxyScriptEngine.AutoDetector.get_CurrentAutoDetector()
                   at System.Net.AutoWebProxyScriptEngine..ctor(WebProxy proxy, Boolean useRegistry)
                   at System.Net.WebProxy.UnsafeUpdateFromRegistry()
                   at System.Net.WebRequest.InternalGetSystemWebProxy()
                   at System.Net.WebRequest.GetSystemWebProxy()
                   at Calamari.Integration.Proxies.ProxyInitializer.InitializeDefaultProxy()
                   at Calamari.Program.Execute(String[] args)
                   at Calamari.Program.Main(String[] args)                 
                 */

                Log.Error("Failed to get the system proxy settings. Calamari will not use any proxy settings.");
            }
        }
    }
}
