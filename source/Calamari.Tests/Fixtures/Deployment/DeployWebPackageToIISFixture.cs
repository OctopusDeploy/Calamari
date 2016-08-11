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
            if (iis.WebSiteExists(uniqueValue)) iis.DeleteWebSite(uniqueValue);
            if (iis.ApplicationPoolExists(uniqueValue)) iis.DeleteApplicationPool(uniqueValue);

            base.CleanUp();
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployAsWebSite()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = "[{\"protocol\":\"http\",\"port\":1082,\"host\":\"\",\"thumbprint\":\"\",\"requireSni\":false,\"enabled\":true}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

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
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployAsVirtualDirectory()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1083);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualDirectory.CreateAsWebApplication"] = "False";

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(package.FilePath);

            result.AssertSuccess();

            var applicationPoolExists = iis.ApplicationPoolExists(uniqueValue);

            Assert.IsFalse(applicationPoolExists);

            var virtualDirectory = iis.FindVirtualDirectory(uniqueValue, ToFirstLevelPath(uniqueValue));

            Assert.AreEqual(ToFirstLevelPath(uniqueValue), virtualDirectory.Path);
            Assert.NotNull(virtualDirectory.PhysicalPath);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldNotAllowNonExistingParentSegments()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1084);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue) + "/" + uniqueValue;

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualDirectory.CreateAsWebApplication"] = "False";

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(package.FilePath);

            result.AssertFailure();
            result.AssertErrorOutput($"Virtual path \"IIS:\\Sites\\{uniqueValue}\\{uniqueValue}\" doesn't exist.");
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployAsWebApplication()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1085);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualDirectory.CreateAsWebApplication"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";


            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(package.FilePath);

            result.AssertSuccess();

            var applicationPool = iis.GetApplicationPool(uniqueValue);

            Assert.AreEqual(uniqueValue, applicationPool.Name);
            Assert.AreEqual(ObjectState.Started, applicationPool.State);
            Assert.AreEqual("v4.0", applicationPool.ManagedRuntimeVersion);
            Assert.AreEqual(ProcessModelIdentityType.ApplicationPoolIdentity, applicationPool.ProcessModel.IdentityType);

            var webApplication = iis.GetWebSite(uniqueValue).Applications.Single(ap => ap.Path == ToFirstLevelPath(uniqueValue));

            Assert.AreEqual(ToFirstLevelPath(uniqueValue), webApplication.Path);
            Assert.NotNull(uniqueValue, webApplication.ApplicationPoolName);
            Assert.NotNull(uniqueValue, webApplication.VirtualDirectories.Single().Path);
        }

        private string ToFirstLevelPath(string value)
        {
            return $"/{value}";
        }
    }
}
