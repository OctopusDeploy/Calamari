using System;
using System.Linq;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Iis;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Microsoft.Web.Administration;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployWebPackageToIISFixture : DeployPackageFixture
    {
        TemporaryFile package;
        private string uniqueValue;
        private WebServerSevenSupport iis;

        [TestFixtureSetUp]
        public void Init()
        {
            package = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0"));
        }

        [TestFixtureTearDown]
        public void Dispose()
        {
            package.Dispose();
        }

        [SetUp]
        public override void SetUp()
        {
            iis = new WebServerSevenSupport();
            uniqueValue = "Test_" + Guid.NewGuid().ToString("N");
            base.SetUp();
        }

        [TearDown]
        public override void CleanUp()
        {
            iis.DeleteWebSite(uniqueValue);
            iis.DeleteApplicationPool(uniqueValue);

            base.CleanUp();
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployWebApplicationToIIS()
        {
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";
            Variables["Octopus.Action.IISWebSite.Bindings"] = "[{\"protocol\":\"http\",\"port\":1082,\"host\":\"\",\"thumbprint\":\"\",\"requireSni\":false,\"enabled\":true}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            $applicationPoolUsername = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolUsername"]
$applicationPoolPassword = $OctopusParameters["Octopus.Action.IISWebSite.ApplicationPoolPassword"]

            //Fail the test firs
            Variables["Octopus.Action.IISWebSite.VirtualFolder.Path"] = $"/{uniqueValue}";
            Variables["Octopus.Action.IISWebSite.VirtualFolder.IsApplication"] = $"/{uniqueValue}";

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(package.FilePath);

            result.AssertSuccess();

            var website = iis.GetWebSite(uniqueValue);

            Assert.AreEqual(uniqueValue, website.Name);
            Assert.AreEqual(ObjectState.Started, website.State);
            Assert.AreEqual(1082, website.Bindings.Single().EndPoint.Port);
            Assert.AreEqual(uniqueValue, website.Applications.First().ApplicationPoolName);

            var applicationPool = iis.GetApplicationPool(uniqueValue);

            Assert.AreEqual(uniqueValue, applicationPool.Name);
            Assert.AreEqual(ObjectState.Started, applicationPool.State);
            Assert.AreEqual("v4.0", applicationPool.ManagedRuntimeVersion);
            Assert.AreEqual(ProcessModelIdentityType.ApplicationPoolIdentity, applicationPool.ProcessModel.IdentityType);

            applicationPool.
        }
    }
}
