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
                var proxyUsername = Environment.GetEnvironmentVariable("TentacleProxyUsername");
                var proxyPassword = Environment.GetEnvironmentVariable("TentacleProxyPassword");
                var proxyHost = Environment.GetEnvironmentVariable("TentacleProxyHost");
                var proxyPortText = Environment.GetEnvironmentVariable("TentacleProxyPort");
                int.TryParse(proxyPortText, out var proxyPort);

                var useSystemProxy = string.IsNullOrWhiteSpace(proxyHost);
                var proxy = useSystemProxy
                    ? WebRequest.GetSystemWebProxy()
                    : new WebProxy(new UriBuilder("http", proxyHost, proxyPort).Uri);
                
                var useDefaultCredentials = string.IsNullOrWhiteSpace(proxyUsername);

                proxy.Credentials = useDefaultCredentials
                    ? useSystemProxy
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential()
                    : new NetworkCredential(proxyUsername, proxyPassword);

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
