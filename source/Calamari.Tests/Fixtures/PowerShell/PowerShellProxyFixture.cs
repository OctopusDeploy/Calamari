using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;
using Calamari.Tests.Helpers;
using Calamari.Util.Environments;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;
using Octostache;
using SetProxy;

namespace Calamari.Tests.Fixtures.PowerShell
{
    [TestFixture]
    public class PowerShellProxyFixture : CalamariFixture
    {
        [SetUp]
        public void Setup()
        {
            ResetProxyEnvironmentVariables();
        }

        [TearDown]
        public void Teardown()
        {
            ResetProxyEnvironmentVariables();
            //ProxyRoutines.SetProxy(false).Should().BeTrue();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyNotSet_ShouldNotSetEnvironmentVariables()
        {
            ResetProxyEnvironmentVariables();
            //ProxyRoutines.SetProxy("http://badproxy:1234").Should().BeTrue();

            EnvironmentHelper.SetEnvironmentVariable("TentacleUseDefaultProxy", false.ToString());
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", "");

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertNoOutput($"Setting Proxy Environment Variables");
            output.AssertPropertyValue("HTTP_PROXY", "");
            output.AssertPropertyValue("HTTPS_PROXY", "");
            //output.AssertPropertyValue("WebRequest.DefaultProxy", "None");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyConfigured_ShouldSetEnvironmentVariables()
        {
            ResetProxyEnvironmentVariables();

            var proxyHost = "hostname";
            var proxyPort = "3456";
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", proxyHost);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", proxyPort);

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertOutputContains($"HTTP_PROXY: http://{proxyHost}:{proxyPort}");
            output.AssertOutputContains($"HTTPS_PROXY: http://{proxyHost}:{proxyPort}");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyWithAuthConfigured_ShouldSetEnvironmentVariables()
        {
            ResetProxyEnvironmentVariables();

            var proxyHost = "hostname";
            var proxyPort = "3456";
            var proxyUsername = "username";
            var proxyPassword = "password";
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", proxyHost);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", proxyPort);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyUsername", proxyUsername);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPassword", proxyPassword);

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertOutputContains($"HTTP_PROXY: http://{proxyUsername}:{proxyPassword}@{proxyHost}:{proxyPort}");
            output.AssertOutputContains($"HTTPS_PROXY: http://{proxyUsername}:{proxyPassword}@{proxyHost}:{proxyPort}");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyEnvironmentAlreadyConfigured_ShouldSetNotSetVariables()
        {
            ResetProxyEnvironmentVariables();

            var proxyHost = "hostname";
            var proxyPort = "3456";
            var httpProxy = "http://proxy:port";
            var httpsProxy = "http://proxy2:port";
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", proxyHost);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", proxyPort);
            EnvironmentHelper.SetEnvironmentVariable("HTTP_PROXY", httpProxy);
            EnvironmentHelper.SetEnvironmentVariable("HTTPS_PROXY", httpsProxy);

            var (output, _) = RunScript("Proxy.ps1");

            output.AssertSuccess();
            output.AssertOutputContains($"HTTP_PROXY: {httpProxy}");
            output.AssertOutputContains($"HTTPS_PROXY: {httpsProxy}");

            ResetProxyEnvironmentVariables();
        }

#if NETFX
        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxySetToSystem_ShouldSetVariablesCorrectly()
        {
            ResetProxyEnvironmentVariables();

            var (output, _) = RunScript("Proxy.ps1");
            var systemProxyUri = System.Net.WebRequest.GetSystemWebProxy().GetProxy(new Uri(@"https://octopus.com"));
            if (systemProxyUri.Host == "octopus.com")
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: ");
                output.AssertOutputContains($"HTTPS_PROXY: ");
            }
            else
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
                output.AssertOutputContains($"HTTPS_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
            }
        }
#endif

#if NETFX
        [Test]
        [Category(TestCategory.CompatibleOS.Windows)]
        public void ProxyNoConfig_ShouldSetNotSetVariables()
        {
            ResetProxyEnvironmentVariables();

            var (output, _) = RunScript("Proxy.ps1");
            var systemProxyUri = System.Net.WebRequest.GetSystemWebProxy().GetProxy(new Uri(@"http://octopus.com"));
            if (systemProxyUri.Host == "octopus.com")
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: ");
                output.AssertOutputContains($"HTTPS_PROXY: ");
            }
            else
            {
                output.AssertSuccess();
                output.AssertOutputContains($"HTTP_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
                output.AssertOutputContains($"HTTPS_PROXY: http://{systemProxyUri.Host}:{systemProxyUri.Port}");
            }
        }
#endif

        void ResetProxyEnvironmentVariables()
        {
            EnvironmentHelper.SetEnvironmentVariable("TentacleUseDefaultProxy", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyHost", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPort", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyUsername", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("TentacleProxyPassword", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("HTTP_PROXY", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("HTTPS_PROXY", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("NO_PROXY", string.Empty);
        }
    }
}