using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Octopus.CoreUtilities;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Integration.Proxies
{

    
    
    public static class ProxyEnvironmentVariablesGenerator
    {
        const string HttpProxyVariableName = "HTTP_PROXY";
        const string HttpsProxyVariableName = "HTTPS_PROXY";
        const string NoProxyVariableName = "NO_PROXY";

        static IEnumerable<string> ProxyEnvironmentVariableNames => 
            new[] {HttpProxyVariableName, HttpsProxyVariableName, NoProxyVariableName}
            .SelectMany(v => new[] { v.ToUpperInvariant(), v.ToLowerInvariant()})
            .ToArray();
        
        public static IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables()
        {
            var environmentVariables = Environment.GetEnvironmentVariables();
            var existingProxyEnvironmentVariables = ProxyEnvironmentVariableNames.Where(environmentVariables.Contains).ToHashSet(StringComparer.Ordinal);
            if (existingProxyEnvironmentVariables.Any())
            {
                Log.Verbose("Proxy related environment variables already exist. Calamari will not overwrite any proxy environment variables.");
                return DuplicateVariablesWithUpperAndLowerCasing(existingProxyEnvironmentVariables, environmentVariables);
            }

            Log.Verbose("Setting Proxy Environment Variables");
            return GenerateProxyEnvironmentVariables(ProxySettingsInitializer.GetProxySettingsFromEnvironment());
        }

        static IEnumerable<EnvironmentVariable> DuplicateVariablesWithUpperAndLowerCasing(ISet<string> existingProxyEnvironmentVariableNames, IDictionary environmentVariables)
        {
            foreach (var existingVariableName in existingProxyEnvironmentVariableNames)
            {
                var requiredVariables = new[] { existingVariableName.ToUpperInvariant(), existingVariableName.ToLowerInvariant() };

                foreach (var requiredVariableName in requiredVariables.Where(v => !existingProxyEnvironmentVariableNames.Contains(v)))
                {
                    yield return new EnvironmentVariable(requiredVariableName, (string)environmentVariables[existingVariableName]);
                }
            }
        }

        static IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables(ProxySettings proxySettings)
        {
            return proxySettings.Accept(new ProxyEnvironmentVariablesVisitor());
        }

        class ProxyEnvironmentVariablesVisitor : IProxySettingsVisitor<IEnumerable<EnvironmentVariable>>
        {
            static readonly Uri TestUri = new Uri("http://test9c7b575efb72442c85f706ef1d64afa6.com");

            public IEnumerable<EnvironmentVariable> Visit(BypassProxySettings proxySettings)
            {
                yield return new EnvironmentVariable(NoProxyVariableName, "*");
            }

            public IEnumerable<EnvironmentVariable> Visit(UseSystemProxySettings proxySettings)
            {
                return SystemWebProxyRetriever.GetSystemWebProxy().SelectValueOr(
                    proxy =>
                    {
                        var proxyUri = proxy.GetProxy(TestUri);

                        return GetProxyEnvironmentVariables(
                            proxyUri.Host,
                            proxyUri.Port,
                            proxySettings.Username,
                            proxySettings.Password);
                    },
                    Enumerable.Empty<EnvironmentVariable>());
            }

            public IEnumerable<EnvironmentVariable> Visit(UseCustomProxySettings proxySettings)
            {
                return GetProxyEnvironmentVariables(
                    proxySettings.Host,
                    proxySettings.Port,
                    proxySettings.Username,
                    proxySettings.Password);
            }

            IEnumerable<EnvironmentVariable> GetProxyEnvironmentVariables(
                string host,
                int port,
                string proxyUsername,
                string proxyPassword)
            {
                string proxyUri;
                if (!string.IsNullOrEmpty(proxyUsername))
                {
#if NET40
                    proxyUri =
                        $"http://{System.Web.HttpUtility.UrlEncode(proxyUsername)}:{System.Web.HttpUtility.UrlEncode(proxyPassword)}@{host}:{port}";
#else
                    proxyUri =
                        $"http://{WebUtility.UrlEncode(proxyUsername)}:{WebUtility.UrlEncode(proxyPassword)}@{host}:{port}";
#endif
                }
                else
                {
                    proxyUri = $"http://{host}:{port}";
                }

                yield return new EnvironmentVariable(HttpProxyVariableName, proxyUri);
                yield return new EnvironmentVariable(HttpProxyVariableName.ToLowerInvariant(), proxyUri);
                yield return new EnvironmentVariable(HttpsProxyVariableName, proxyUri);
                yield return new EnvironmentVariable(HttpsProxyVariableName.ToLowerInvariant(), proxyUri);
                yield return new EnvironmentVariable(NoProxyVariableName, "127.0.0.1,localhost,169.254.169.254");
                yield return new EnvironmentVariable(NoProxyVariableName.ToLowerInvariant(), "127.0.0.1,localhost,169.254.169.254");
            }
        }
    }
}
