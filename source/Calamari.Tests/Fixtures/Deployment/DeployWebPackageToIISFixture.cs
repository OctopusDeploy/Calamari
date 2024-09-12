#if IIS_SUPPORT
using System;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.FullFrameworkTools.Iis;
using Calamari.Integration.Iis;
using Calamari.Testing.Helpers;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers.Certificates;
using Microsoft.Web.Administration;
using NUnit.Framework;
using Polly;
using FluentAssertions;

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

#if WINDOWS_CERTIFICATE_STORE_SUPPORT
            SampleCertificate.CapiWithPrivateKey.EnsureCertificateNotInStore(StoreName.My, StoreLocation.LocalMachine);
#endif

            base.SetUp();
        }

        [TearDown]
        public override void CleanUp()
        {
            if (iis.WebSiteExists(uniqueValue)) iis.DeleteWebSite(uniqueValue);
            if (iis.ApplicationPoolExists(uniqueValue)) iis.DeleteApplicationPool(uniqueValue);

#if WINDOWS_CERTIFICATE_STORE_SUPPORT
            SampleCertificate.CapiWithPrivateKey.EnsureCertificateNotInStore(StoreName.My, StoreLocation.LocalMachine);
#endif

            base.CleanUp();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
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

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

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
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldDeployAsVirtualDirectory()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1083);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var applicationPoolExists = ApplicationPoolExists(uniqueValue);

            Assert.IsFalse(applicationPoolExists);

            var virtualDirectory = FindVirtualDirectory(uniqueValue, ToFirstLevelPath(uniqueValue));

            Assert.AreEqual(ToFirstLevelPath(uniqueValue), virtualDirectory.Path);
            Assert.IsTrue(virtualDirectory.PhysicalPath.Contains("1.0.0"));
        }


        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldKeepExistingBindingsWhenExistingBindingsIsMerge()
        {
            // The variable we are testing
            Variables["Octopus.Action.IISWebSite.ExistingBindings"] = "merge";

            const int existingBindingPort = 1083;
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", existingBindingPort);
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = @"[{""protocol"":""http"",""port"":1082,""host"":"""",""thumbprint"":"""",""requireSni"":false,""enabled"":true}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            website.Bindings.Should().Contain(b => b.EndPoint.Port == existingBindingPort, "Existing binding should remain");
            website.Bindings.Should().Contain(b => b.EndPoint.Port == 1082, "New binding should be added");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldExcludeTempBindingWhenExistingBindingsIsMerge()
        {
            // The variable we are testing
            Variables["Octopus.Action.IISWebSite.ExistingBindings"] = "merge";

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = @"[{""protocol"":""http"",""port"":1082,""host"":"""",""thumbprint"":"""",""requireSni"":false,""enabled"":true}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            website.Bindings.Count().Should().Be(1);
            website.Bindings.Should().Contain(b => b.EndPoint.Port == 1082, "New binding should be added");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldRemoveExistingBindingsByDefault()
        {
            const int existingBindingPort = 1083;
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", existingBindingPort);
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = @"[{""protocol"":""http"",""port"":1082,""host"":"""",""thumbprint"":"""",""requireSni"":false,""enabled"":true}]";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            website.Bindings.Should().NotContain(b => b.EndPoint.Port == existingBindingPort);
            website.Bindings.Should().Contain(b => b.EndPoint.Port == 1082);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldDeployNewVersion()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1083);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            result = DeployPackage(packageV2.FilePath);

            result.AssertSuccess();

            var virtualDirectory = FindVirtualDirectory(uniqueValue, ToFirstLevelPath(uniqueValue));

            Assert.IsTrue(virtualDirectory.PhysicalPath.Contains("2.0.0"));
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldNotAllowMissingParentSegments()
        {
            iis.CreateWebSiteOrVirtualDirectory(uniqueValue, null, ".", 1084);

            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue) + "/" + uniqueValue;

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualDirectory.CreateAsWebApplication"] = "False";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertFailure();
            result.AssertErrorOutput($"Virtual path \"IIS:\\Sites\\{uniqueValue}\\{uniqueValue}\" does not exist.", true);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldNotAllowMissingWebSiteForVirtualFolders()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "virtualDirectory";
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.CreateOrUpdate"] = "True";

            Variables["Octopus.Action.IISWebSite.VirtualDirectory.WebSiteName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.VirtualDirectory.VirtualPath"] = ToFirstLevelPath(uniqueValue);

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertFailure();
            result.AssertErrorOutput($"The Web Site \"{uniqueValue}\" does not exist", true);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
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


            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

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
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
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

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertFailure();

            result.AssertErrorOutput("Please delete", true);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
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

                Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

                var result = DeployPackage(packageV1.FilePath);

                result.AssertSuccess();
            }
        }


#if WINDOWS_CERTIFICATE_STORE_SUPPORT
        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
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
            Variables["AcmeSelfSigned.Thumbprint"] = SampleCertificate.CapiWithPrivateKey.Thumbprint;
            Variables["AcmeSelfSigned.Pfx"] = SampleCertificate.CapiWithPrivateKey.Base64Bytes();
            Variables["AcmeSelfSigned.Password"] = SampleCertificate.CapiWithPrivateKey.Password;

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            var binding = website.Bindings.Single();

            Assert.AreEqual(1083, binding.EndPoint.Port);
            Assert.AreEqual("https", binding.Protocol);
            Assert.AreEqual(SampleCertificate.CapiWithPrivateKey.Thumbprint, BitConverter.ToString(binding.CertificateHash).Replace("-", ""));
            Assert.IsTrue(binding.CertificateStoreName.Equals("My", StringComparison.OrdinalIgnoreCase), $"Expected: 'My'. Received: '{binding.CertificateStoreName}'");

            // Assert the application-pool account was granted access to the certificate private-key
            var certificate = SampleCertificate.CapiWithPrivateKey.GetCertificateFromStore("MY", StoreLocation.LocalMachine);
            SampleCertificate.AssertIdentityHasPrivateKeyAccess(certificate, new NTAccount("IIS AppPool\\" + uniqueValue), CryptoKeyRights.GenericAll);

            Assert.AreEqual(ObjectState.Started, website.State);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldFindAndUseExistingCertificateInStoreIfPresent()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = "[{\"protocol\":\"https\",\"port\":1084,\"host\":\"\",\"certificateVariable\":\"AcmeSelfSigned\",\"requireSni\":false,\"enabled\":true}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables["AcmeSelfSigned"] = "Certificates-1";
            Variables["AcmeSelfSigned.Type"] = "Certificate";
            Variables["AcmeSelfSigned.Thumbprint"] = SampleCertificate.CapiWithPrivateKey.Thumbprint;
            Variables["AcmeSelfSigned.Pfx"] = SampleCertificate.CapiWithPrivateKey.Base64Bytes();

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            SampleCertificate.CapiWithPrivateKey.EnsureCertificateIsInStore(StoreName.My, StoreLocation.LocalMachine);

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            var binding = website.Bindings.Single();

            Assert.AreEqual(1084, binding.EndPoint.Port);
            Assert.AreEqual("https", binding.Protocol);
            Assert.AreEqual(SampleCertificate.CapiWithPrivateKey.Thumbprint, BitConverter.ToString(binding.CertificateHash).Replace("-", ""));
            Assert.AreEqual(StoreName.My.ToString(), binding.CertificateStoreName);

            Assert.AreEqual(ObjectState.Started, website.State);

            SampleCertificate.CapiWithPrivateKey.EnsureCertificateNotInStore(StoreName.My, StoreLocation.LocalMachine);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldCreateHttpsBindingUsing_SingleCertificatePassedAsThumbprint()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] =
                $"[{{\"protocol\":\"https\",\"port\":1083,\"host\":\"\",\"thumbprint\":\"{SampleCertificate.CapiWithPrivateKey.Thumbprint}\",\"requireSni\":false,\"enabled\":true}}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            SampleCertificate.CapiWithPrivateKey.EnsureCertificateIsInStore(StoreName.My, StoreLocation.LocalMachine);

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            var binding = website.Bindings.Single();

            Assert.AreEqual(1083, binding.EndPoint.Port);
            Assert.AreEqual("https", binding.Protocol);
            Assert.AreEqual(SampleCertificate.CapiWithPrivateKey.Thumbprint, BitConverter.ToString(binding.CertificateHash).Replace("-", ""));
            Assert.IsTrue(binding.CertificateStoreName.Equals("My", StringComparison.OrdinalIgnoreCase), $"Expected: 'My'. Received: '{binding.CertificateStoreName}'");

            Assert.AreEqual(ObjectState.Started, website.State);
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldCreateHttpsBindingUsing_MultipleCertificates_InMultipleStores_PassedAsThumbprint()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] =
                $"[{{\"protocol\":\"https\",\"port\":1083,\"host\":\"\",\"thumbprint\":\"{SampleCertificate.CapiWithPrivateKey.Thumbprint}\",\"requireSni\":false,\"enabled\":true}}, "
                + $"{{\"protocol\":\"https\",\"port\":1084,\"host\":\"\",\"thumbprint\":\"{SampleCertificate.CngWithPrivateKey.Thumbprint}\",\"requireSni\":false,\"enabled\":true}}]";

            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";

            SampleCertificate.CapiWithPrivateKey.EnsureCertificateIsInStore(StoreName.My, StoreLocation.LocalMachine);
            SampleCertificate.CngWithPrivateKey.EnsureCertificateIsInStore(StoreName.CertificateAuthority, StoreLocation.LocalMachine);

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();

            var website = GetWebSite(uniqueValue);
            var binding = website.Bindings;

            var capiBinding = binding.First();
            var cngBinding = binding.Skip(1).First();

            Assert.AreEqual(1083, capiBinding.EndPoint.Port);
            Assert.AreEqual(1084, cngBinding.EndPoint.Port);

            Assert.AreEqual("https", capiBinding.Protocol);
            Assert.AreEqual("https", cngBinding.Protocol);
            
            Assert.AreEqual(SampleCertificate.CapiWithPrivateKey.Thumbprint, BitConverter.ToString(capiBinding.CertificateHash).Replace("-", ""));
            Assert.AreEqual(SampleCertificate.CngWithPrivateKey.Thumbprint, BitConverter.ToString(cngBinding.CertificateHash).Replace("-", ""));

            Assert.IsTrue(capiBinding.CertificateStoreName.Equals("My", StringComparison.OrdinalIgnoreCase), $"Expected: 'My'. Received: '{capiBinding.CertificateStoreName}'");
            Assert.IsTrue(cngBinding.CertificateStoreName.Equals("CA", StringComparison.OrdinalIgnoreCase), $"Expected: 'CA'. Received: '{cngBinding.CertificateStoreName}'");

            Assert.AreEqual(ObjectState.Started, website.State);
        }

        [Test] // https://github.com/OctopusDeploy/Issues/issues/3378
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldNotFailIfDisabledBindingUsesUnavailableCertificateVariable()
        {
            Variables["Octopus.Action.IISWebSite.DeploymentType"] = "webSite";
            Variables["Octopus.Action.IISWebSite.CreateOrUpdateWebSite"] = "True";

            Variables["Octopus.Action.IISWebSite.Bindings"] = "[{\"protocol\":\"http\",\"port\":1082,\"host\":\"\",\"thumbprint\":\"\",\"requireSni\":false,\"enabled\":true},{\"protocol\":\"https\",\"port\":1084,\"host\":\"\",\"certificateVariable\":\"AcmeSelfSigned\",\"requireSni\":false,\"enabled\":#{HTTPS Binding Enabled}}]";
            Variables["Octopus.Action.IISWebSite.EnableAnonymousAuthentication"] = "True";
            Variables["Octopus.Action.IISWebSite.EnableBasicAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.EnableWindowsAuthentication"] = "False";
            Variables["Octopus.Action.IISWebSite.WebSiteName"] = uniqueValue;

            Variables["Octopus.Action.IISWebSite.ApplicationPoolName"] = uniqueValue;
            Variables["Octopus.Action.IISWebSite.ApplicationPoolFrameworkVersion"] = "v4.0";
            Variables["Octopus.Action.IISWebSite.ApplicationPoolIdentityType"] = "ApplicationPoolIdentity";

            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.IISWebSite";
            Variables["HTTPS Binding Enabled"] = "false";

            var result = DeployPackage(packageV1.FilePath);

            result.AssertSuccess();
        }
#endif

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
