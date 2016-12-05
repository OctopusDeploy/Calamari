#if IIS_SUPPORT
using System;
using System.IO;
using System.Linq;
using Calamari.Deployment;
using Calamari.Extensibility;
using Calamari.Extensibility.IIS;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Microsoft.Web.Administration;
using NUnit.Framework;
using Polly;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployWebPackageToIISFixture : DeployPackageFixture
    {
        TemporaryFile packageV1;
        TemporaryFile packageV2;
        private string uniqueValue;
        private WebServerSevenSupport iis;
      

        [OneTimeSetUp]
        public void Init()
        {
            packageV1 = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0"));
            packageV2 = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "2.0.0"));
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            packageV1.Dispose();
            packageV2.Dispose();
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

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);

            Assert.AreEqual(uniqueValue, website.Name);
            Assert.AreEqual(ObjectState.Started, website.State);
            Assert.AreEqual(1082, website.Bindings.Single().EndPoint.Port);
            var application = website.Applications.First();
            Assert.AreEqual(uniqueValue, application.ApplicationPoolName);
            Assert.IsTrue(application.VirtualDirectories.Single().PhysicalPath.Contains("1.0.0"));

            var applicationPool = GetApplicationPool(uniqueValue);

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

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";
            
            Console.WriteLine($"XXXXXXXXXXXXX - {packageV1.FilePath}");
            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            Console.WriteLine($"XXXXXXXXXXXXX - {uniqueValue}");
            var applicationPoolExists = ApplicationPoolExists(uniqueValue);

            Assert.IsFalse(applicationPoolExists);

            var virtualDirectory = FindVirtualDirectory(uniqueValue, ToFirstLevelPath(uniqueValue));

            Assert.AreEqual(ToFirstLevelPath(uniqueValue), virtualDirectory.Path);
            Assert.IsTrue(virtualDirectory.PhysicalPath.Contains("1.0.0"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployNewVersion()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1083);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            result = DeployPackage(packageV2.FilePath);

            result.AssertSuccess();

            var virtualDirectory = FindVirtualDirectory(uniqueValue, ToFirstLevelPath(uniqueValue));

            Assert.IsTrue(virtualDirectory.PhysicalPath.Contains("2.0.0"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldNotAllowMissingParentSegments()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1084);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue) + "/" + uniqueValue;

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualDirectory.CreateAsWebApplication"] = "False";

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertFailure();
            result.AssertErrorOutput($"Virtual path \"IIS:\\Sites\\{uniqueValue}\\{uniqueValue}\" does not exist.", true);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldNotAllowMissingWebSiteForVirtualFolders()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertFailure();
            result.AssertErrorOutput($"The Web Site \"{uniqueValue}\" does not exist", true);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployAsWebApplication()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1085);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webApplication";
            Variables["Octopus.Action.IISWebSite.WebApplication.CreateOrUpdate"] = "True";
                                                 
            Variables["Octopus.Action.IISWebSite.WebApplication.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.WebApplication.VirtualPath"] = ToFirstLevelPath(uniqueValue);
                                                 
            Variables["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";


            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var applicationPool = GetApplicationPool(uniqueValue);

            Assert.AreEqual(uniqueValue, applicationPool.Name);
            Assert.AreEqual(ObjectState.Started, applicationPool.State);
            Assert.AreEqual("v4.0", applicationPool.ManagedRuntimeVersion);
            Assert.AreEqual(ProcessModelIdentityType.ApplicationPoolIdentity, applicationPool.ProcessModel.IdentityType);

            var webApplication = FindWebApplication(uniqueValue, ToFirstLevelPath(uniqueValue));

            Assert.AreEqual(ToFirstLevelPath(uniqueValue), webApplication.Path);
            Assert.AreEqual(uniqueValue, webApplication.ApplicationPoolName);
            Assert.IsTrue(webApplication.VirtualDirectories.Single().PhysicalPath.Contains("1.0.0"));
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDetectAttemptedConversionFromVirtualDirectoryToWebApplication()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, $"/{uniqueValue}", ".", 1086);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webApplication";
            Variables["Octopus.Action.IISWebSite.WebApplication.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.WebApplication.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.WebApplication.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.WebApplication.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertFailure();

            result.AssertErrorOutput("Please delete", true);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldDeployWhenVirtualPathAlreadyExistsAndPointsToPhysicalDirectory()
        {
            var webSitePhysicalPath = Path.Combine(Path.GetTempPath(), uniqueValue);
            Directory.CreateDirectory(webSitePhysicalPath);
            using (new TemporaryDirectory(webSitePhysicalPath))
            {
                iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, webSitePhysicalPath, 1087);

                Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
                Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

                Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
                Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

                Variables["Octopus.Action.Package.CustomInstallationDirectory"] = Path.Combine(webSitePhysicalPath,
                    uniqueValue);
                Variables["Octopus.Action.Package.CustomInstallationDirectoryShouldBePurgedBeforeDeployment"] = "True";

                Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

                var result = DeployPackage(packageV1.FilePath);

                result.AssertSuccess();
            }
        }

        private string ToFirstLevelPath(string value)
        {
            return $"/{value}";
        }

        private Site GetWebSite(string webSiteName)
        {
            return Retry(() => iis.GetWebSite(webSiteName));
        }

        private ApplicationPool GetApplicationPool(string applicationPoolNae)
        {
            return Retry(() => iis.GetApplicationPool(applicationPoolNae));
        }

        private VirtualDirectory FindVirtualDirectory(string webSiteName, string path)
        {
            return Retry(() => iis.FindVirtualDirectory(webSiteName, path));
        }

        private bool ApplicationPoolExists(string applicationPoolName)
        {
            return Retry(() => iis.ApplicationPoolExists(applicationPoolName));
        }

        private TResult Retry<TResult>(Func<TResult> func)
        {
            return Policy.Handle<Exception>()
                        .WaitAndRetry(5, retry => TimeSpan.FromSeconds(retry), (e, _) =>
                        {
                            Console.WriteLine("Retry failed.");
                        })
                        .Execute(func);
        }

        private Application FindWebApplication(string websiteName, string virtualPath)
        {
            return GetWebSite(websiteName).Applications.Single(ap => ap.Path == virtualPath);
        }
    }
}
#endif
