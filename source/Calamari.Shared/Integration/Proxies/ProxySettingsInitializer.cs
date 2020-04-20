using System;

namespace Calamari.Integration.Proxies
{
    public static class ProxySettingsInitializer
    {
        public static IProxySettings GetProxySettingsFromEnvironment()
        {
            var proxyUsername = Environment.GetEnvironmentVariable(DeploymentEnvironmentVariables.TentacleProxyUsername);
            var proxyPassword = Environment.GetEnvironmentVariable(DeploymentEnvironmentVariables.TentacleProxyPassword);
            var proxyHost = Environment.GetEnvironmentVariable(DeploymentEnvironmentVariables.TentacleProxyHost);
            var proxyPortText = Environment.GetEnvironmentVariable(DeploymentEnvironmentVariables.TentacleProxyPort);
            int.TryParse(proxyPortText, out var proxyPort);
            var useCustomProxy = !string.IsNullOrWhiteSpace(proxyHost);

            if (useCustomProxy)
            {
                return new UseCustomProxySettings(
                    proxyHost,
                    proxyPort,
                    proxyUsername,
                    proxyPassword
                );
            }

            bool useDefaultProxy;
            if (!bool.TryParse(Environment.GetEnvironmentVariable(DeploymentEnvironmentVariables.TentacleUseDefaultProxy),
                out useDefaultProxy))
                useDefaultProxy = true;

            if (useDefaultProxy)
            {
                return new UseSystemProxySettings(
                    proxyUsername,
                    proxyPassword);
            }

            return new BypassProxySettings();
        }
    }
}