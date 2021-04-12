using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Octopus.CoreUtilities;

namespace Calamari.Common.Plumbing.Proxies
{
    public interface IProxySettings
    {
        Maybe<IWebProxy> CreateProxy();
        IEnumerable<EnvironmentVariable> GenerateEnvironmentVariables();
    }

    public class BypassProxySettings : IProxySettings
    {
        public Maybe<IWebProxy> CreateProxy()
        {
            return new WebProxy().AsSome<IWebProxy>();
        }

        public IEnumerable<EnvironmentVariable> GenerateEnvironmentVariables()
        {
            yield return new EnvironmentVariable(ProxyEnvironmentVariablesGenerator.NoProxyVariableName, "*");
        }
    }

    public class UseSystemProxySettings : IProxySettings
    {
        static readonly Uri TestUri = new Uri("http://proxytestingdomain.octopus.com");

        public UseSystemProxySettings(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public string Username { get; }
        public string Password { get; }

        public Maybe<IWebProxy> CreateProxy()
        {
            return SystemWebProxyRetriever.GetSystemWebProxy()
                .Select(proxy =>
                {
                    proxy.Credentials = string.IsNullOrWhiteSpace(Username)
                        ? CredentialCache.DefaultNetworkCredentials
                        : new NetworkCredential(Username, Password);

                    return proxy;
                });
        }

        public IEnumerable<EnvironmentVariable> GenerateEnvironmentVariables()
        {
            return SystemWebProxyRetriever.GetSystemWebProxy()
                .SelectValueOr(
                    proxy =>
                    {
                        var proxyUri = proxy.GetProxy(TestUri);

                        return ProxyEnvironmentVariablesGenerator.GetProxyEnvironmentVariables(
                            proxyUri.Host,
                            proxyUri.Port,
                            Username,
                            Password);
                    },
                    Enumerable.Empty<EnvironmentVariable>()
                );
        }
    }

    public class UseCustomProxySettings : IProxySettings
    {
        public UseCustomProxySettings(string host, int port, string username, string password)
        {
            Host = host;
            Port = port;
            Username = username;
            Password = password;
        }

        public string Host { get; }
        public int Port { get; }
        public string Username { get; }
        public string Password { get; }

        public Maybe<IWebProxy> CreateProxy()
        {
            var proxy = new WebProxy(new UriBuilder("http", Host, Port).Uri)
            {
                Credentials = string.IsNullOrWhiteSpace(Username)
                    ? new NetworkCredential()
                    : new NetworkCredential(Username, Password)
            };

            return proxy.AsSome<IWebProxy>();
        }

        public IEnumerable<EnvironmentVariable> GenerateEnvironmentVariables()
        {
            return ProxyEnvironmentVariablesGenerator.GetProxyEnvironmentVariables(
                Host,
                Port,
                Username,
                Password
            );
        }
    }
}