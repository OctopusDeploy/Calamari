using System;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Proxies
{
    [TestFixture]
    public abstract class ScriptProxyFixtureBase : CalamariFixture
    {
        protected const string BadproxyUrl = "http://proxy-initializer-fixture-bad-proxy:1234";
        protected string ProxyUserName = "some@:/user";
        protected string ProxyPassword = "some@:/password";

#if NET40
        const string UrlEncodedProxyUserName = "some%40%3a%2fuser";
        const string UrlEncodedProxyPassword = "some%40%3a%2fpassword";
#else
        protected static string UrlEncodedProxyUserName = "some%40%3A%2Fuser";
        protected static string UrlEncodedProxyPassword = "some%40%3A%2Fpassword";
#endif
        
        protected const string proxyHost = "proxy-initializer-fixture-good-proxy";
        protected const int proxyPort = 8888;

        protected string proxyUrl = $"http://{proxyHost}:{proxyPort}";
        protected string authenticatedProxyUrl = $"http://{UrlEncodedProxyUserName}:{UrlEncodedProxyPassword}@{proxyHost}:{proxyPort}";

        protected static bool IsRunningOnWindows = CalamariEnvironment.IsRunningOnWindows;
        
        [TearDown]
        public void TearDown()
        {
            ResetProxyEnvironmentVariables();
        }

        [Test]
        public virtual void Initialize_NoSystemProxy_NoProxy()
        {
            var result = RunWith(false, "", 80, "", "");

            AssertProxyBypassed(result);
        }

        [Test]
        public virtual void Initialize_NoSystemProxy_UseSystemProxy()
        {
            var result = RunWith(true, "", 80, "", "");

            AssertNoProxyChanges(result);
        }

        [Test]
        public virtual void Initialize_NoSystemProxy_UseSystemProxyWithCredentials()
        {
            var result = RunWith(true, "", 80, ProxyUserName, ProxyPassword);

            AssertNoProxyChanges(result);
        }

        [Test]
        public virtual void Initialize_NoSystemProxy_CustomProxy()
        {
            var result = RunWith(false, proxyHost, proxyPort, "", "");

            AssertUnauthenticatedProxyUsed(result);
        }

        [Test]
        public virtual void Initialize_NoSystemProxy_CustomProxyWithCredentials()
        {
            var result = RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed(result);
        }

        protected CalamariResult RunWith(
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

            return RunScript();
        }

        protected abstract CalamariResult RunScript();

        void ResetProxyEnvironmentVariables()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleUseDefaultProxy, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyUsername, string.Empty);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPassword, string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("HTTP_PROXY", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("HTTPS_PROXY", string.Empty);
            EnvironmentHelper.SetEnvironmentVariable("NO_PROXY", string.Empty);
        }

        protected virtual void AssertAuthenticatedProxyUsed(CalamariResult output)
        {
            output.AssertSuccess();
            output.AssertPropertyValue("HTTP_PROXY", authenticatedProxyUrl);
            output.AssertPropertyValue("HTTPS_PROXY", authenticatedProxyUrl);
            output.AssertPropertyValue("NO_PROXY", "127.0.0.1,localhost,169.254.169.254");
        }

        protected virtual void AssertUnauthenticatedProxyUsed(CalamariResult output)
        {
            output.AssertSuccess();
            output.AssertPropertyValue("HTTP_PROXY", proxyUrl);
            output.AssertPropertyValue("HTTPS_PROXY", proxyUrl);
            output.AssertPropertyValue("NO_PROXY", "127.0.0.1,localhost,169.254.169.254");
        }

        protected virtual void AssertNoProxyChanges(CalamariResult output)
        {
            output.AssertSuccess();
            output.AssertPropertyValue("HTTP_PROXY", "");
            output.AssertPropertyValue("HTTPS_PROXY", "");
            output.AssertPropertyValue("NO_PROXY", "");
        }

        protected virtual void AssertProxyBypassed(CalamariResult output)
        {
            output.AssertSuccess();
            output.AssertPropertyValue("HTTP_PROXY", "");
            output.AssertPropertyValue("HTTPS_PROXY", "");
            output.AssertPropertyValue("NO_PROXY", "*");
        }
    }
}