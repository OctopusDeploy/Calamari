using System;
using System.Net;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Proxies
{
    [TestFixture]
    public class ProxySettingsInitializerFixture
    {
        const string ProxyUserName = "someuser";
        const string ProxyPassword = "some@://password";
        string proxyHost = "proxy-initializer-fixture-good-proxy";
        int proxyPort = 1234;

        [TearDown]
        public void TearDown()
        {
            ResetProxyEnvironmentVariables();
        }

        [Test]
        public void Initialize_BypassProxy()
        {
            SetEnvironmentVariables(false, "", 80, "", "");

            AssertBypassProxy(ProxySettingsInitializer.GetProxySettingsFromEnvironment());
        }

        [Test]
        public void Initialize_UseSystemProxy()
        {
            SetEnvironmentVariables(true, "", 80, "", "");

            AssertSystemProxySettings(ProxySettingsInitializer.GetProxySettingsFromEnvironment(), false);
        }

        [Test]
        public void Initialize_UseSystemProxyWithCredentials()
        {
            SetEnvironmentVariables(true, "", 80, ProxyUserName, ProxyPassword);

            AssertSystemProxySettings(ProxySettingsInitializer.GetProxySettingsFromEnvironment(), true);
        }

        [Test]
        public void Initialize_CustomProxy()
        {
            SetEnvironmentVariables(false, proxyHost, proxyPort, "", "");

            AssertCustomProxy(ProxySettingsInitializer.GetProxySettingsFromEnvironment(), false);
        }

        [Test]
        public void Initialize_CustomProxyWithCredentials()
        {
            SetEnvironmentVariables(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertCustomProxy(ProxySettingsInitializer.GetProxySettingsFromEnvironment(), true);
        }

        void SetEnvironmentVariables(
            bool useDefaultProxy,
            string proxyhost,
            int proxyPort,
            string proxyUsername,
            string proxyPassword)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy,
                useDefaultProxy.ToString());
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, proxyhost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, proxyPort.ToString());
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, proxyUsername);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, proxyPassword);

        }

        void ResetProxyEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, string.Empty);
        }

        void AssertCustomProxy(IProxySettings proxySettings, bool hasCredentials)
        {
            var proxy = proxySettings.Should().BeOfType<UseCustomProxySettings>()
                .Subject;

            proxy.Host.Should().Be(proxyHost);
            proxy.Port.Should().Be(proxyPort);

            if (hasCredentials)
            {
                proxy.Username.Should().Be(ProxyUserName);
                proxy.Password.Should().Be(ProxyPassword);
            }
            else
            {
                proxy.Username.Should().BeNull();
                proxy.Password.Should().BeNull();
            }
        }

        static void AssertSystemProxySettings(IProxySettings proxySettings, bool hasCredentials)
        {
            var proxy = proxySettings.Should().BeOfType<UseSystemProxySettings>()
                .Subject;
            
            if (hasCredentials)
            {
                proxy.Username.Should().Be(ProxyUserName);
                proxy.Password.Should().Be(ProxyPassword);
            }
            else
            {
                proxy.Username.Should().BeNull();
                proxy.Password.Should().BeNull();
            }
        }

        void AssertBypassProxy(IProxySettings proxySettings)
        {
            proxySettings.Should().BeOfType<BypassProxySettings>();
        }
    }
}
