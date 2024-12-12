using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Common.Plumbing.Proxies
{
    public static class ProxyEnvironmentVariablesGenerator
    {
        public const string HttpProxyVariableName = "HTTP_PROXY";
        public const string HttpsProxyVariableName = "HTTPS_PROXY";
        public const string NoProxyVariableName = "NO_PROXY";

        static IEnumerable<string> ProxyEnvironmentVariableNames =>
            new[]
                {
                    HttpProxyVariableName, HttpsProxyVariableName, NoProxyVariableName
                }
                .SelectMany(v => new[]
                {
                    v.ToUpperInvariant(), v.ToLowerInvariant()
                })
                .ToArray();

        public static IEnumerable<EnvironmentVariable> GenerateProxyEnvironmentVariables()
        {
            var environmentVariables = Environment.GetEnvironmentVariables();
            var existingProxyEnvironmentVariables = new HashSet<string>(ProxyEnvironmentVariableNames.Where(environmentVariables.Contains), StringComparer.Ordinal);
            if (existingProxyEnvironmentVariables.Any())
            {
                Log.Verbose("Proxy related environment variables already exist. Calamari will not overwrite any proxy environment variables.");
                return DuplicateVariablesWithUpperAndLowerCasing(existingProxyEnvironmentVariables, environmentVariables);
            }

            Log.Verbose("Setting Proxy Environment Variables");
            return ProxySettingsInitializer.GetProxySettingsFromEnvironment().GenerateEnvironmentVariables();
        }

        static IEnumerable<EnvironmentVariable> DuplicateVariablesWithUpperAndLowerCasing(ISet<string> existingProxyEnvironmentVariableNames, IDictionary environmentVariables)
        {
            foreach (var existingVariableName in existingProxyEnvironmentVariableNames)
            {
                var requiredVariables = new[]
                {
                    existingVariableName.ToUpperInvariant(), existingVariableName.ToLowerInvariant()
                };

                foreach (var requiredVariableName in requiredVariables.Where(v => !existingProxyEnvironmentVariableNames.Contains(v)))
                    yield return new EnvironmentVariable(requiredVariableName, (string)environmentVariables[existingVariableName]);
            }
        }

        public static IEnumerable<EnvironmentVariable> GetProxyEnvironmentVariables(
            string host,
            int port,
            string proxyUsername,
            string proxyPassword)
        {
            var proxyUri = !string.IsNullOrEmpty(proxyUsername) ?
                $"http://{WebUtility.UrlEncode(proxyUsername)}:{WebUtility.UrlEncode(proxyPassword)}@{host}:{port}" :
                $"http://{host}:{port}";

            yield return new EnvironmentVariable(HttpProxyVariableName, proxyUri);
            yield return new EnvironmentVariable(HttpProxyVariableName.ToLowerInvariant(), proxyUri);
            yield return new EnvironmentVariable(HttpsProxyVariableName, proxyUri);
            yield return new EnvironmentVariable(HttpsProxyVariableName.ToLowerInvariant(), proxyUri);
            yield return new EnvironmentVariable(NoProxyVariableName, "127.0.0.1,localhost,169.254.169.254");
            yield return new EnvironmentVariable(NoProxyVariableName.ToLowerInvariant(), "127.0.0.1,localhost,169.254.169.254");
        }
    }
}