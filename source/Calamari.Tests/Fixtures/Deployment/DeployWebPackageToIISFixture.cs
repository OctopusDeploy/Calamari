using System;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Iis;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Calamari.Tests.Helpers.Certificates;
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
      
        [TestFixtureSetUp]
        public void Init()
        {
            packageV1 = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0"));
            packageV2 = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "2.0.0"));
        }

        [TestFixtureTearDown]
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
            SampleCertificate.CapiWithPrivateKeyNoPassword.EnsureCertificateNotInStore(StoreName.My, StoreLocation.LocalMachine);
            base.SetUp();
        }

        [TearDown]
        public override void CleanUp()
        {
            if (iis.WebSiteExists(uniqueValue)) iis.DeleteWebSite(uniqueValue);
            if (iis.ApplicationPoolExists(uniqueValue)) iis.DeleteApplicationPool(uniqueValue);
            SampleCertificate.CapiWithPrivateKeyNoPassword.EnsureCertificateNotInStore(StoreName.My, StoreLocation.LocalMachine);

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

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

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
            result.AssertErrorOutput($"Site \"{uniqueValue}\" does not exist.", true);
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
        public void ShouldCreateHttpsBindingUsingCertificatePassedAsVariable()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = "[{\"protocol\":\"https\",\"port\":1083,\"host\":\"\",\"certificateVariable\":\"AcmeSelfSigned\",\"requireSni\":false,\"enabled\":true}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables["AcmeSelfSigned"] = "Certificates-1";
            Variables["AcmeSelfSigned.Type"] = "Certificate";
            Variables["AcmeSelfSigned.Thumbprint"] = SampleCertificate.CapiWithPrivateKeyNoPassword.Thumbprint;
            Variables["AcmeSelfSigned.Pfx"] = SampleCertificate.CapiWithPrivateKeyNoPassword.Base64Bytes();

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            var binding = website.Bindings.Single();

            Assert.AreEqual(1083, binding.EndPoint.Port);
            Assert.AreEqual("https", binding.Protocol);
            Assert.AreEqual(SampleCertificate.CapiWithPrivateKeyNoPassword.Thumbprint, BitConverter.ToString(binding.CertificateHash).Replace("-", ""));
            Assert.AreEqual("MY", binding.CertificateStoreName);

            // Assert the application-pool account was granted access to the certificate private-key
            var certificate = SampleCertificate.CapiWithPrivateKeyNoPassword.GetCertificateFromStore("MY", StoreLocation.LocalMachine); 
            SampleCertificate.AssertIdentityHasPrivateKeyAccess(certificate, new NTAccount("IIS AppPool\\" + uniqueValue), CryptoKeyRights.GenericAll);

            Assert.AreEqual(ObjectState.Started, website.State);
        }

        [Test]
        [Category(TestEnvironment.CompatibleOS.Windows)]
        public void ShouldCreateHttpsBindingUsingCertificatePassedAsThumbprint()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] =
                $"[{{\"protocol\":\"https\",\"port\":1083,\"host\":\"\",\"thumbprint\":\"{SampleCertificate.CapiWithPrivateKeyNoPassword.Thumbprint}\",\"requireSni\":false,\"enabled\":true}}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            SampleCertificate.CapiWithPrivateKeyNoPassword.EnsureCertificateIsInStore(StoreName.My, StoreLocation.LocalMachine);

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            var binding = website.Bindings.Single();

            Assert.AreEqual(1083, binding.EndPoint.Port);
            Assert.AreEqual("https", binding.Protocol);
            Assert.AreEqual(SampleCertificate.CapiWithPrivateKeyNoPassword.Thumbprint, BitConverter.ToString(binding.CertificateHash).Replace("-", ""));
            Assert.True(binding.CertificateStoreName.Equals("MY", StringComparison.OrdinalIgnoreCase));

            Assert.AreEqual(ObjectState.Started, website.State);
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
