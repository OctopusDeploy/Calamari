using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Iis;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployWebPackageFixture : DeployPackageFixture
    {
        // Fixture Depedencies
        TemporaryFile nupkgFile;
        TemporaryFile tarFile;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }

        [OneTimeSetUp]
        public void Init()
        {
            nupkgFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0"));
            tarFile = new TemporaryFile(TarGzBuilder.BuildSamplePackage("Acme.Web", "1.0.0"));
        }


        [OneTimeTearDown]
        public void Dispose()
        {
            nupkgFile.Dispose();
            tarFile.Dispose();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldDeployPackageOnWindows()
        {
            var result = DeployPackage();
            result.AssertSuccess();

            result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, "Acme.Web", "1.0.0"));

            result.AssertOutput("Extracted 11 files");
            result.AssertOutput("Hello from Deploy.ps1");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void ShouldDeployPackageOnMacOrNix()
        {
            if (!CalamariEnvironment.IsRunningOnMac && !CalamariEnvironment.IsRunningOnNix)
                Assert.Inconclusive("This test is designed to run on *Nix or Mac.");

            var result = DeployPackage();
            result.AssertSuccess();

            result.AssertOutput("Hello from Deploy.sh");
        }

        [Test]
        public void ShouldSetExtractionVariable()
        {
            var result = DeployPackage();
            result.AssertSuccess();
            result.AssertOutputVariable(PackageVariables.Output.InstallationDirectoryPath, Is.EqualTo(Path.Combine(StagingDirectory, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldCopyToCustomDirectoryExtractionVariable()
        {
            Variables[PackageVariables.CustomInstallationDirectory] = CustomDirectory;
            var result = DeployPackage();
            result.AssertSuccess();
            result.AssertOutput("Copying package contents to");
            result.AssertOutputVariable(PackageVariables.Output.InstallationDirectoryPath, Is.EqualTo(CustomDirectory));
        }

        [Test]
        public void ShouldSubstituteVariablesInFiles()
        {
            Variables.Set("foo", "bar");
            // Enable file substitution and configure the target
            Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
            Variables.Set(PackageVariables.SubstituteInFilesTargets, "web.config");

            DeployPackage();

            // The #{foo} variable in web.config should have been replaced by 'bar'
            AssertXmlNodeValue(Path.Combine(StagingDirectory, "Acme.Web", "1.0.0", "web.config"), "configuration/appSettings/add[@key='foo']/@value", "bar");
        }

        [Test]
        public void ShouldSubstituteVariablesInRelativePathFiles()
        {
            Variables.Set("foo", "bar");

            var path = Path.Combine("assets", "README.txt");

            // Enable file substitution and configure the target
            Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.SubstituteInFiles);
            Variables.Set(PackageVariables.SubstituteInFilesTargets, path);

            DeployPackage();

            // The #{foo} variable in assets\README.txt should have been replaced by 'bar'
            string actual = FileSystem.ReadFile(Path.Combine(StagingDirectory, "Acme.Web", "1.0.0", "assets", "README.txt"));
            Assert.AreEqual("bar", actual);
        }

        [Test]
        public void ShouldTransformConfig()
        {
            // Set the environment, and the flag to automatically run config transforms
            Variables.Set(DeploymentEnvironment.Name, "Production");
            Variables.Set(KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());
            Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.ConfigurationTransforms);

            var result = DeployPackage();

            // The environment app-setting value should have been transformed to 'Production'
            AssertXmlNodeValue(Path.Combine(StagingDirectory, "Production", "Acme.Web", "1.0.0", "web.config"), "configuration/appSettings/add[@key='environment']/@value", "Production");
        }

        [Test]
        [Category(TestCategory.ScriptingSupport.FSharp)]
        [Category(TestCategory.ScriptingSupport.DotnetScript)]
        public void ShouldInvokeDeployFailedOnError()
        {
            Variables.Set("ShouldFail", "yes");
            var result = DeployPackage();
            if (ScriptingEnvironment.IsRunningOnMono())
                result.AssertOutput("I have failed! DeployFailed.sh");
            else
                result.AssertOutput("I have failed! DeployFailed.ps1");
            result.AssertNoOutput("I have failed! DeployFailed.fsx");
            result.AssertNoOutput("I have failed! DeployFailed.csx");
        }

        public void ShouldNotInvokeDeployFailedWhenNoError()
        {
            var result = DeployPackage();
            result.AssertNoOutput("I have failed! DeployFailed.ps1");
            result.AssertNoOutput("I have failed! DeployFailed.sh");
            result.AssertNoOutput("I have failed! DeployFailed.fsx");
            result.AssertNoOutput("I have failed! DeployFailed.csx");
        }

        [Test]
        [TestCase(DeploymentType.Nupkg)]
        [TestCase(DeploymentType.Tar)]
        public void ShouldCopyFilesToCustomInstallationDirectory(DeploymentType deploymentType)
        {
            // Set-up a custom installation directory
            string customInstallDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestInstall");
            FileSystem.EnsureDirectoryExists(customInstallDirectory);
            // Ensure the directory is empty before we start
            FileSystem.PurgeDirectory(customInstallDirectory, FailureOptions.ThrowOnFailure);
            Variables.Set(PackageVariables.CustomInstallationDirectory, customInstallDirectory );

            var result = DeployPackage(deploymentType);

            // Assert content was copied to custom-installation directory
            Assert.IsTrue(FileSystem.FileExists(Path.Combine(customInstallDirectory, "assets", "styles.css")));
        }

        [Test]
        [TestCase(DeploymentType.Nupkg)]
        [TestCase(DeploymentType.Tar)]
        public void ShouldExecuteFeatureScripts(DeploymentType deploymentType)
        {
            Variables.Set(KnownVariables.Package.EnabledFeatures, "Octopus.Features.HelloWorld");
            var result = DeployPackage(deploymentType);
            result.AssertOutput("Hello World!");
        }

#if IIS_SUPPORT
        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldModifyIisWebsiteRoot()
        {
            // If the 'UpdateIisWebsite' variable is set, the website root will be updated

            // Create the website
            var originalWebRootPath = Path.Combine(Path.GetTempPath(), "CalamariTestIisSite");
            FileSystem.EnsureDirectoryExists(originalWebRootPath);
            var webServer = WebServerSupport.AutoDetect();
            var siteName = "CalamariTest-" + Guid.NewGuid();
            webServer.CreateWebSiteOrVirtualDirectory(siteName, "/", originalWebRootPath, 1081);

            Variables.Set(SpecialVariables.Package.UpdateIisWebsite, true.ToString());
            Variables.Set(SpecialVariables.Package.UpdateIisWebsiteName, siteName);

            var result = DeployPackage();

            Assert.AreEqual(
                Path.Combine(StagingDirectory, "Acme.Web\\1.0.0"),
                webServer.GetHomeDirectory(siteName, "/"));

            // And remove the website
            webServer.DeleteWebSite(siteName);
            FileSystem.DeleteDirectory(originalWebRootPath);
        }
#endif

        [Test]
        public void ShouldRunConfiguredScripts()
        {
            Variables.Set(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);

            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
            {
                Variables.Set(ConfiguredScriptConvention.GetScriptName(DeploymentStages.Deploy, ScriptSyntax.Bash), "echo 'The wheels on the bus go round...'");
            }
            else
            {
                Variables.Set(ConfiguredScriptConvention.GetScriptName(DeploymentStages.Deploy, ScriptSyntax.PowerShell), "Write-Host 'The wheels on the bus go round...'");
            }

            var result = DeployPackage();

            result.AssertOutput("The wheels on the bus go round...");
        }

        [Test]
        public void ShouldAddJournalEntry()
        {
            var result = DeployPackage();

            result.AssertOutput("Adding journal entry");
        }

        [Test]
        [TestCase(DeploymentType.Nupkg)]
        [TestCase(DeploymentType.Tar)]
        public void ShouldSkipIfAlreadyInstalled(DeploymentType deploymentType)
        {
            Variables.Set(KnownVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            Variables.Set(KnownVariables.RetentionPolicySet, "a/b/c/d");
            Variables.Set(PackageVariables.PackageId, "Acme.Web");
            Variables.Set(PackageVariables.PackageVersion, "1.0.0");

            var result = DeployPackage(deploymentType);
            result.AssertSuccess();
            result.AssertOutput("The package has been installed to");

            result = DeployPackage(deploymentType);
            result.AssertSuccess();
            result.AssertOutput("The package has already been installed on this machine");
        }

        [Test]
        public void ShouldSkipIfAlreadyInstalledWithDifferentPackageType()
        {
            Variables.Set(KnownVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            Variables.Set(KnownVariables.RetentionPolicySet, "a/b/c/d");
            Variables.Set(PackageVariables.PackageId, "Acme.Web");
            Variables.Set(PackageVariables.PackageVersion, "1.0.0");

            var result = DeployPackage(DeploymentType.Tar);
            result.AssertSuccess();
            result.AssertOutput("The package has been installed to");

            result = DeployPackage(DeploymentType.Nupkg);
            result.AssertSuccess();
            result.AssertOutput("The package has already been installed on this machine");
        }

        [Test]
        public void ShouldDeployInParallel()
        {
            var locker = new object();
            var extractionDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var errors = new List<Exception>();

            var threads = Enumerable.Range(0, 4).Select(i => new Thread(new ThreadStart(delegate
            {
                try
                {
                    CalamariResult result;
                    using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
                    {
                        lock (locker) // this save method isn't thread safe
                        {
                            Variables.Save(variablesFile.FilePath);
                        }

                        result = Invoke(Calamari()
                            .Action("deploy-package")
                            .Argument("package", nupkgFile.FilePath)
                            .Argument("variables", variablesFile.FilePath));
                    }

                    result.AssertSuccess();
                    var extracted = result.GetOutputForLineContaining("Extracting package to: ");
                    result.AssertOutput("Extracted 11 files");

                    lock (locker)
                    {
                        if (!extractionDirectories.Contains(extracted))
                        {
                            extractionDirectories.Add(extracted);
                        }
                        else
                        {
                            Assert.Fail("The same installation directory was used twice: " + extracted);
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors.Add(ex);
                }
            }))).ToList();

            foreach (var thread in threads) thread.Start();
            foreach (var thread in threads) thread.Join();

            var allErrors = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
            Assert.That(allErrors, Is.EqualTo(""), allErrors);
        }

        CalamariResult DeployPackage(DeploymentType deploymentType = DeploymentType.Nupkg)
        {
            var packageName = deploymentType == DeploymentType.Nupkg ? nupkgFile.FilePath : tarFile.FilePath;
            return DeployPackage(packageName);
        }

        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }

        private void AssertXmlNodeValue(string xmlFile, string nodeXPath, string value)
        {
            var configXml = new XmlDocument();
            configXml.LoadXml( FileSystem.ReadFile(xmlFile));
            var node = configXml.SelectSingleNode(nodeXPath);

            Assert.AreEqual(value, node.Value);
        }

        public enum DeploymentType
        {
            Tar,
            Nupkg
        }
    }
}
