using Microsoft.IdentityModel.Clients.ActiveDirectory;
using NUnit.Framework;

namespace Calamari.Azure.ServiceFabric.Tests.Fixtures
{
    [TestFixture]
    public class AzurePowerShellFixture
    {
        /// <summary>
        /// We need a specific version of Microsoft.IdentityModel.Clients.ActiveDirectory for our Service Fabric
        /// PowerShell, as Microsoft moved all the cheesee. If you update this library without realising the
        /// consequences, SF steps will suddenly stop working from PS :/
        /// </summary>
        [Test]
        public void ShouldUseActiveDirectoryListSpecificVersion()
        {
            var version = typeof(AuthenticationContext).Assembly.GetName().Version;
            Assert.AreEqual("2.28.3.860", version.ToString());
        }
    }
}