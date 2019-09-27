using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using SetProxy;

namespace Calamari.Tests.Fixtures.Integration.Proxies
{
    [TestFixture]
    public abstract class WindowsScriptProxyFixtureBase : ScriptProxyFixtureBase
    {
        protected abstract bool TestWebRequestDefaultProxy { get; }
        
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
                output.AssertPropertyValue("WebRequest.DefaultProxy", proxyUrl + "/");
        }

        protected override void AssertUnauthenticatedProxyUsed(CalamariResult output)
        {
            base.AssertUnauthenticatedProxyUsed(output);
            if (IsRunningOnWindows && TestWebRequestDefaultProxy)
                output.AssertPropertyValue("WebRequest.DefaultProxy", proxyUrl + "/");
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

        void AssertUnauthenticatedSystemProxyUsed(CalamariResult output)
        {
#if !NETCORE
            AssertUnauthenticatedProxyUsed(output);
#else
            base.AssertNoProxyChanges(output);
#endif
        }
        
        void AssertAuthenticatedSystemProxyUsed(CalamariResult output)
        {
#if !NETCORE
            AssertAuthenticatedProxyUsed(output);
#else
            base.AssertNoProxyChanges(output);
#endif
        }
    }
}