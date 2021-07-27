using System;
using System.Net;
using System.Net.Sockets;
using Calamari.Common.Plumbing.Logging;
using Octopus.CoreUtilities;

namespace Calamari.Common.Plumbing.Proxies
{
    public static class SystemWebProxyRetriever
    {
        public static Maybe<IWebProxy> GetSystemWebProxy()
        {
            try
            {
                var TestUri = new Uri("http://test9c7b575efb72442c85f706ef1d64afa6.com");

                var systemWebProxy = WebRequest.GetSystemWebProxy();

                return systemWebProxy.GetProxy(TestUri).Host != TestUri.Host
                    ? systemWebProxy.AsSome()
                    : Maybe<IWebProxy>.None;
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
                return Maybe<IWebProxy>.None;
            }
        }
    }
}