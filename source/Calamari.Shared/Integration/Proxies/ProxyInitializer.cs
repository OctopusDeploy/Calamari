using System;
using System.Net;
using Octopus.CoreUtilities;

namespace Calamari.Integration.Proxies
{
    public static class ProxyInitializer
    {
        public static void InitializeDefaultProxy()
        {
            InitializeDefaultProxy(ProxySettingsInitializer.GetProxySettingsFromEnvironment());
        }

        static void InitializeDefaultProxy(ProxySettings proxySettings)
        {
            Maybe<IWebProxy> proxy = proxySettings.Accept(new WebProxyVisitor());
            
            if (proxy.Some())
                WebRequest.DefaultWebProxy = proxy.Value;
        }


        class WebProxyVisitor : IProxySettingsVisitor<Maybe<IWebProxy>>
        {
            public Maybe<IWebProxy> Visit(BypassProxySettings proxySettings)
            {
                IWebProxy emptyProxy = new WebProxy();
                return emptyProxy.AsSome();
            }

            public Maybe<IWebProxy> Visit(UseSystemProxySettings proxySettings)
            {
                return SystemWebProxyRetriever.GetSystemWebProxy().Select(proxy =>
                {
                    proxy.Credentials = string.IsNullOrWhiteSpace(proxySettings.Username)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(proxySettings.Username, proxySettings.Password);

                    return proxy;
                });
            }

            public Maybe<IWebProxy> Visit(UseCustomProxySettings proxySettings)
            {
                IWebProxy proxy = new WebProxy(new UriBuilder("http", proxySettings.Host, proxySettings.Port).Uri);
                proxy.Credentials = string.IsNullOrWhiteSpace(proxySettings.Username)
                    ? new NetworkCredential()
                    : new NetworkCredential(proxySettings.Username, proxySettings.Password);

                return proxy.AsSome();
            }
        }
    }
}