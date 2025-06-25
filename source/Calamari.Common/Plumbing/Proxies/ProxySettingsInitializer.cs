using System;

namespace Calamari.Common.Plumbing.Proxies
{
    public static class ProxySettingsInitializer
    {
        public static IProxySettings GetProxySettingsFromEnvironment()
        {
            var proxyUsername = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername);
            var proxyPassword = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword);
            var proxyHost = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost);
            var proxyPortText = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort);
            int.TryParse(proxyPortText, out var proxyPort);
            var useCustomProxy = !string.IsNullOrWhiteSpace(proxyHost);

            if (useCustomProxy)
                return new UseCustomProxySettings(
                    proxyHost,
                    proxyPort,
                    proxyUsername,
                    proxyPassword
                );

            if (!bool.TryParse(Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy), out var useDefaultProxy))
                useDefaultProxy = true;

            if (useDefaultProxy)
                return new UseSystemProxySettings(
                    proxyUsername,
                    proxyPassword);

            return new BypassProxySettings();
        }
    }
}