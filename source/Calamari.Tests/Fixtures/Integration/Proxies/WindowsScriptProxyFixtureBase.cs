using System;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Integration.Proxies
{
    [TestFixture]
    public abstract class WindowsScriptProxyFixtureBase : ScriptProxyFixtureBase
    {
        protected abstract bool TestWebRequestDefaultProxy { get; }

        protected CalamariResult RunWith(bool useDefaultProxy, string proxyhost, int proxyPort, string proxyUsername, string proxyPassword, string proxyException)
        {
            Environment.SetEnvironmentVariable("TEST_ONLY_PROXY_EXCEPTION_URI", proxyException);

            return RunWith(useDefaultProxy, proxyhost, proxyPort, proxyUsername, proxyPassword);
        }

        [TearDown]
        public void ResetSystemProxy()
        {
            if (IsRunningOnWindows)
                ProxyRoutines.SetProxy(false).Should().BeTrue();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public virtual void Initialize_HasSystemProxy_NoProxy()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            var result = RunWith(false, "", 80, "", "");

            AssertProxyBypassed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public virtual void Initialize_HasSystemProxy_UseSystemProxy()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            var result = RunWith(true, "", 80, "", "");

            AssertUnauthenticatedSystemProxyUsed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public virtual void Initialize_HasSystemProxy_UseSystemProxyWithExceptions()
        {
            var proxyException = "octopustestbypassurl.com";
            var proxyExceptionUrl = $"http://{proxyException}/";
            ProxyRoutines.SetProxy(proxyUrl, proxyException).Should().BeTrue();
            var result = RunWith(true, "", 80, "", "", proxyExceptionUrl);

            AssertUnauthenticatedSystemProxyUsedWithException(result, proxyExceptionUrl);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public virtual void Initialize_HasSystemProxy_UseSystemProxyWithCredentials()
        {
            ProxyRoutines.SetProxy(proxyUrl).Should().BeTrue();
            var result = RunWith(true, "", 80, ProxyUserName, ProxyPassword);

            AssertAuthenticatedSystemProxyUsed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public virtual void Initialize_HasSystemProxy_CustomProxy()
        {
            ProxyRoutines.SetProxy(BadproxyUrl).Should().BeTrue();
            var result = RunWith(false, proxyHost, proxyPort, "", "");

            AssertUnauthenticatedProxyUsed(result);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public virtual void Initialize_HasSystemProxy_CustomProxyWithCredentials()
        {
            ProxyRoutines.SetProxy(BadproxyUrl).Should().BeTrue();
            var result = RunWith(false, proxyHost, proxyPort, ProxyUserName, ProxyPassword);

            AssertAuthenticatedProxyUsed(result);
        }

        protected override void AssertAuthenticatedProxyUsed(CalamariResult output)
        {
            base.AssertAuthenticatedProxyUsed(output);
            if (IsRunningOnWindows && TestWebRequestDefaultProxy)
                // This can be either the authenticated or unauthenticated URL. The authentication part should be ignored
                output.AssertPropertyValue("WebRequest.DefaultProxy", proxyUrl + "/", authenticatedProxyUrl + "/");
        }

        protected override void AssertUnauthenticatedProxyUsed(CalamariResult output)
        {
            base.AssertUnauthenticatedProxyUsed(output);
            if (IsRunningOnWindows && TestWebRequestDefaultProxy)
                // This can be either the authenticated or unauthenticated URL. The authentication part should be ignored
                output.AssertPropertyValue("WebRequest.DefaultProxy", proxyUrl + "/", authenticatedProxyUrl + "/");
        }

        protected override void AssertNoProxyChanges(CalamariResult output)
        {
            base.AssertNoProxyChanges(output);
            if (IsRunningOnWindows && TestWebRequestDefaultProxy)
                output.AssertPropertyValue("WebRequest.DefaultProxy", "None");
        }

        protected override void AssertProxyBypassed(CalamariResult output)
        {
            base.AssertProxyBypassed(output);
            if (IsRunningOnWindows && TestWebRequestDefaultProxy)
                output.AssertPropertyValue("WebRequest.DefaultProxy", "None");
        }

        void AssertUnauthenticatedSystemProxyUsedWithException(CalamariResult output, string bypassedUrl)
        {
            AssertUnauthenticatedSystemProxyUsed(output);
            if (TestWebRequestDefaultProxy)
                output.AssertPropertyValue("ProxyBypassed", bypassedUrl);
        }

        void AssertUnauthenticatedSystemProxyUsed(CalamariResult output)
        {
            AssertUnauthenticatedProxyUsed(output);
        }

        void AssertAuthenticatedSystemProxyUsed(CalamariResult output)
        {
            AssertAuthenticatedProxyUsed(output);
        }
    }
}