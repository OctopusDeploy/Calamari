using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures.Integration.Proxies;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Python
{
    [TestFixture]
    public class PythonProxyFixture : WindowsScriptProxyFixtureBase
    {
        [RequiresMinimumPython3Version(4)]
        public override void Initialize_NoSystemProxy_NoProxy()
        {
            base.Initialize_NoSystemProxy_NoProxy();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_NoSystemProxy_CustomProxy()
        {
            base.Initialize_NoSystemProxy_CustomProxy();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_NoSystemProxy_CustomProxyWithCredentials()
        {
            base.Initialize_NoSystemProxy_CustomProxyWithCredentials();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_NoSystemProxy_UseSystemProxy()
        {
            base.Initialize_NoSystemProxy_UseSystemProxy();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_NoSystemProxy_UseSystemProxyWithCredentials()
        {
            base.Initialize_NoSystemProxy_UseSystemProxyWithCredentials();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_HasSystemProxy_NoProxy()
        {
            base.Initialize_HasSystemProxy_NoProxy();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_HasSystemProxy_CustomProxy()
        {
            base.Initialize_HasSystemProxy_CustomProxy();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_HasSystemProxy_CustomProxyWithCredentials()
        {
            base.Initialize_HasSystemProxy_CustomProxyWithCredentials();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_HasSystemProxy_UseSystemProxy()
        {
            base.Initialize_HasSystemProxy_UseSystemProxy();
        }

        [RequiresMinimumPython3Version(4)]
        public override void Initialize_HasSystemProxy_UseSystemProxyWithCredentials()
        {
            base.Initialize_HasSystemProxy_UseSystemProxyWithCredentials();
        }

        protected override CalamariResult RunScript()
        {
            return RunScript("proxy.py").result;
        }

        protected override bool TestWebRequestDefaultProxy => false;
    }
}