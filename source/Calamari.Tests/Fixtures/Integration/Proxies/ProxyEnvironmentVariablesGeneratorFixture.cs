using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using NUnit.Framework.Constraints;

namespace Calamari.Tests.Fixtures.Integration.Proxies
{
    [TestFixture]
    public class ProxyEnvironmentVariablesGeneratorFixture
    {
        const string BadproxyUrl = "http://proxy-initializer-fixture-bad-proxy:1234";
        const string ProxyUserName = "some@:/user";
        const string ProxyPassword = "some@:/password";

        const string UrlEncodedProxyUserName = "some%40%3A%2Fuser";
        const string UrlEncodedProxyPassword = "some%40%3A%2Fpassword";
        const string proxyHost = "proxy-initializer-fixture-good-proxy";
        const int proxyPort = 8888;
        
        string proxyUrl = $"http://{proxyHost}:{proxyPort}";
        string authentiatedProxyUrl = $"http://{UrlEncodedProxyUserName}:{UrlEncodedProxyPassword}@{proxyHost}:{proxyPort}";

        [TearDown]
        public void TearDown()
        {
            ResetProxyEnvironmentVariables();
            ResetSystemProxy();
        }

        static void ResetSystemProxy()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                ProxyRoutines.SetProxy(false).Should().BeTrue();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void Initialize_HasSystemProxy_NoProxy()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            var result = RunWith(false, "", 80, "", "");

            AssertProxyBypassed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void Initialize_HasSystemProxy_UseSystemProxy()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            var result = RunWith(true, "", 80, "", "");

            AssertUnauthenticatedSystemProxyUsed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void Initialize_HasSystemProxy_UseSystemProxyWithCredentials()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            var result = RunWith(true, "", 80, ProxyUserName, ProxyPassword);

            AssertAuthenticatedSystemProxyUsed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void Initialize_HasSystemProxy_CustomProxy()
        {
            ProxyRoutines.SetProxy(BadproxyUrl).Should().BeTrue();
            var result = RunWith(false, proxyHost, proxyPort, "", "");

            AssertUnauthenticatedProxyUsed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void Initialize_HasSystemProxy_CustomProxyWithCredentials()
        {
            ProxyRoutines.SetProxy(BadproxyUrl).Should().BeTrue();
            var result = RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed(result);
        }

        [Test]
        public void Initialize_NoSystemProxy_NoProxy()
        {
            var result = RunWith(false, "", 80, "", "");

            AssertProxyBypassed(result);
        }

        [Test]
        public void Initialize_NoSystemProxy_UseSystemProxy()
        {
            var result = RunWith(true, "", 80, "", "");

            AssertNoProxyChanges(result);
        }

        [Test]
        public void Initialize_NoSystemProxy_UseSystemProxyWithCredentials()
        {
            var result = RunWith(true, "", 80, ProxyUserName, ProxyPassword);

            AssertNoProxyChanges(result);
        }

        [Test]
        public void Initialize_NoSystemProxy_CustomProxy()
        {
            var result = RunWith(false, proxyHost, proxyPort, "", "");

            AssertUnauthenticatedProxyUsed(result);
        }

        [Test]
        public void Initialize_NoSystemProxy_CustomProxyWithCredentials()
        {
            var result = RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed(result);
        }

        [TestCase("http_proxy")]
        [TestCase("https_proxy")]
        [TestCase("no_proxy")]
        public void Initialize_OneLowerCaseEnvironmentVariableExists_UpperCaseVariantReturned(string existingVariableName)
        {
            var existingValue = "blahblahblah";
            Environment.SetEnvironmentVariable(existingVariableName, existingValue);
            var result = RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword).ToList();

            result.Should().ContainSingle("The existing variable should be duplicated as an upper case variable");
            var variable = result.Single();
            variable.Key.Should().Be(existingVariableName.ToUpperInvariant());
            variable.Value.Should().Be(existingValue);
        }
        
        [TestCase("HTTP_PROXY")]
        [TestCase("HTTPS_PROXY")]
        [TestCase("NO_PROXY")]
        public void Initialize_OneUpperCaseEnvironmentVariableExists_LowerCaseVariantReturned(string existingVariableName)
        {
            var existingValue = "blahblahblah";
            Environment.SetEnvironmentVariable(existingVariableName, existingValue);
            var result = RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword).ToList();

            result.Should().ContainSingle("The existing variable should be duplicated as a lower case variable");
            var variable = result.Single();
            variable.Key.Should().Be(existingVariableName.ToLowerInvariant());
            variable.Value.Should().Be(existingValue);
        }
        
        IEnumerable<EnvironmentVariable> RunWith(
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

            return ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables();
        }

        void ResetProxyEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, string.Empty);
            Environment.SetEnvironmentVariable("HTTP_PROXY", string.Empty);
            Environment.SetEnvironmentVariable("http_proxy", string.Empty);
            Environment.SetEnvironmentVariable("HTTPS_PROXY", string.Empty);
            Environment.SetEnvironmentVariable("https_proxy", string.Empty);
            Environment.SetEnvironmentVariable("NO_PROXY", string.Empty);
            Environment.SetEnvironmentVariable("no_proxy", string.Empty);
        }

        void AssertAuthenticatedProxyUsed(IEnumerable<EnvironmentVariable> result)
        {
            var httpProxy = result.Should().ContainSingle(kv => kv.Key == "HTTP_PROXY").Subject;
            var httpsProxy = result.Should().ContainSingle(kv => kv.Key == "HTTPS_PROXY").Subject;
            var noProxy = result.Should().ContainSingle(kv => kv.Key == "NO_PROXY").Subject;

            httpProxy.Value.Should().Be(authentiatedProxyUrl, "should use the proxy");
            httpsProxy.Value.Should().Be(authentiatedProxyUrl, "should use the proxy");
            noProxy.Value.Should().Be("127.0.0.1,localhost,169.254.169.254", "should use the proxy");
        }

        void AssertUnauthenticatedProxyUsed(IEnumerable<EnvironmentVariable> result)
        {
            var httpProxy = result.Should().ContainSingle(kv => kv.Key == "HTTP_PROXY").Subject;
            var httpsProxy = result.Should().ContainSingle(kv => kv.Key == "HTTPS_PROXY").Subject;
            var noProxy = result.Should().ContainSingle(kv => kv.Key == "NO_PROXY").Subject;

            httpProxy.Value.Should().Be(proxyUrl, "should use the proxy");
            httpsProxy.Value.Should().Be(proxyUrl, "should use the proxy");
            noProxy.Value.Should().Be("127.0.0.1,localhost,169.254.169.254", "should use the proxy");
        }

        void AssertNoProxyChanges(IEnumerable<EnvironmentVariable> result)
        {
            result.Should().NotContain(kv => kv.Key == "HTTP_PROXY");
            result.Should().NotContain(kv => kv.Key == "HTTPS_PROXY");
            result.Should().NotContain(kv => kv.Key == "NO_PROXY");
        }

        void AssertProxyBypassed(IEnumerable<EnvironmentVariable> result)
        {
            result.Should().NotContain(kv => kv.Key == "HTTP_PROXY");
            result.Should().NotContain(kv => kv.Key == "HTTPS_PROXY");
            var noProxy = result.Should().ContainSingle(kv => kv.Key == "NO_PROXY").Subject;

            noProxy.Value.Should().Be("*", "should bypass the proxy");
        }
        
        void AssertUnauthenticatedSystemProxyUsed(IEnumerable<EnvironmentVariable> output)
        {
#if !NETCORE
            AssertUnauthenticatedProxyUsed(output);
#else
            AssertNoProxyChanges(output);
#endif
        }
        
        void AssertAuthenticatedSystemProxyUsed(IEnumerable<EnvironmentVariable> output)
        {
#if !NETCORE
            Console.WriteLine("AssertAuthenticatedSystemProxyUsed Running without NETCORE");
#else
                Console.WriteLine("AssertAuthenticatedSystemProxyUsed Running with NETCORE");
#endif                

#if !NETCORE
            AssertAuthenticatedProxyUsed(output);
#else
            AssertNoProxyChanges(output);
#endif
        }
    }
}
