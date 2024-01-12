using System;
using System.Net;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Proxies
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
    [Ignore("Testing assumption that proxy is causing havoc")]
    public class ProxyInitializerFixture
    {
        const string BadproxyUrl = "http://proxy-initializer-fixture-bad-proxy:1234";
        const string WebRequestUrl = "http://octopus.com";
        const string ProxyUserName = "someuser";
        const string ProxyPassword = "some@://password";
        string proxyHost;
        int proxyPort;
        string proxyUrl;

        IWebProxy defaultWebProxy;

        [SetUp]
        public void Setup()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                defaultWebProxy = WebRequest.DefaultWebProxy;

            proxyHost = "proxy-initializer-fixture-good-proxy";
            proxyPort = 8888;
            proxyUrl = $"http://{proxyHost}:{proxyPort}";
        }

        [TearDown]
        public void TearDown()
        {
            ResetProxyEnvironmentVariables();

            if (CalamariEnvironment.IsRunningOnWindows)
                WebRequest.DefaultWebProxy = defaultWebProxy;

            ResetSystemProxy();
        }

        static void ResetSystemProxy()
        {
            if (CalamariEnvironment.IsRunningOnWindows)
                ProxyRoutines.SetProxy(false).Should().BeTrue();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_HasSystemProxy_NoProxy()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            RunWith(false, "", 80, "", "");

            AssertProxyNotUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_HasSystemProxy_UseSystemProxy()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            RunWith(true, "", 80, "", "");

            AssertUnauthenticatedSystemProxyUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_HasSystemProxy_UseSystemProxyWithCredentials()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            RunWith(true, "", 80, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_HasSystemProxy_CustomProxy()
        {
            ProxyRoutines.SetProxy(BadproxyUrl).Should().BeTrue();
            RunWith(false, proxyHost, proxyPort, "", "");

            AssertUnauthenticatedProxyUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_HasSystemProxy_CustomProxyWithCredentials()
        {
            ProxyRoutines.SetProxy(BadproxyUrl).Should().BeTrue();
            RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_NoSystemProxy_NoProxy()
        {
            RunWith(false, "", 80, "", "");

            AssertProxyNotUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_NoSystemProxy_UseSystemProxy()
        {
            RunWith(true, "", 80, "", "");

            AssertProxyNotUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_NoSystemProxy_UseSystemProxyWithCredentials()
        {
            RunWith(true, "", 80, ProxyUserName, ProxyPassword);

            AssertProxyNotUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_NoSystemProxy_CustomProxy()
        {
            RunWith(false, proxyHost, proxyPort, "", "");

            AssertUnauthenticatedProxyUsed();
        }

        [Test]
        [RequiresDotNetFramework]
        public void Initialize_NoSystemProxy_CustomProxyWithCredentials()
        {
            RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed();
        }

        void RunWith(
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

            ProxyInitializer.InitializeDefaultProxy();
        }

        void ResetProxyEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, string.Empty);
        }

        void AssertAuthenticatedProxyUsed()
        {
            AssertProxyUsed();

            var credentials = WebRequest.DefaultWebProxy.Credentials.Should().BeOfType<NetworkCredential>().Subject;
            credentials.UserName.Should().Be(ProxyUserName);
            credentials.Password.Should().Be(ProxyPassword);
        }

        void AssertUnauthenticatedProxyUsed()
        {
            AssertProxyUsed();
            var credentials = WebRequest.DefaultWebProxy.Credentials.Should().BeOfType<NetworkCredential>().Subject;
            credentials.UserName.Should().Be("");
            credentials.Password.Should().Be("");
        }

        void AssertUnauthenticatedSystemProxyUsed()
        {
            AssertProxyUsed();
            var credentials = WebRequest.DefaultWebProxy.Credentials.Should().BeAssignableTo<NetworkCredential>()
                .Subject;
            credentials.GetType().Name.Should()
                .Be("SystemNetworkCredential"); //It's internal so we can't access the type
            credentials.UserName.Should().Be("");
            credentials.Password.Should().Be("");
        }

        void AssertProxyUsed()
        {
            var uri = new Uri(WebRequestUrl);
            WebRequest.DefaultWebProxy.GetProxy(uri).Should().Be(new Uri(proxyUrl), "should use the proxy");
        }

        static void AssertProxyNotUsed()
        {
            var uri = new Uri(WebRequestUrl);
            WebRequest.DefaultWebProxy.GetProxy(uri).Should().Be(uri, "shouldn't use the proxy");
        }
    }
}
