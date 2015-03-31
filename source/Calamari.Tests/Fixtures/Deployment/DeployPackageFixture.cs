using System;
using System.IO;
using System.Xml;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Integration.Iis;
using Calamari.Tests.Helpers;
using NSubstitute;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployPackageFixture : CalamariFixture
    {
        string stagingDirectory;
        ICalamariFileSystem fileSystem;
        VariableDictionary variables;
        CalamariResult result;

        [SetUp]
        public void SetUp()
        {
            fileSystem = new CalamariPhysicalFileSystem();

            // Ensure staging directory exists and is empty 
            stagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            fileSystem.EnsureDirectoryExists(stagingDirectory);
            fileSystem.PurgeDirectory(stagingDirectory, DeletionOptions.TryThreeTimes);

            variables = new VariableDictionary();
            variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, stagingDirectory);
            variables.Set("PreDeployGreeting", "Bonjour");
        }

        [Test]
        public void ShouldDeployPackage()
        {
            result = DeployPackage("Acme.Web");
            result.AssertZero();

            result.AssertOutput("Extracting package to: " + stagingDirectory + "\\Acme.Web\\1.0.0");
            result.AssertOutput("Extracted 6 files");

            result.AssertOutput("Bonjour from PreDeploy.ps1");
        }

        [Test]
        public void ShouldSubstituteVariablesInFiles()
        {
            variables.Set("foo", "bar");
            // Enable file substitution and configure the target
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, "web.config");

            result = DeployPackage("Acme.Web");

            // The #{foo} variable in web.config should have been replaced by 'bar'
            AssertXmlNodeValue(stagingDirectory + "\\Acme.Web\\1.0.0\\web.config", "configuration/appSettings/add[@key='foo']/@value", "bar");
        }

        [Test]
        public void ShouldTransformConfig()
        {
            // Set the environment, and the flag to automatically run config transforms
            variables.Set(SpecialVariables.Environment.Name, "Production");
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());

            result = DeployPackage("Acme.Web");

            // The environment app-setting value should have been transformed to 'Production'
            AssertXmlNodeValue(stagingDirectory + "\\Production\\Acme.Web\\1.0.0\\web.config", "configuration/appSettings/add[@key='environment']/@value", "Production");
        }

        [Test]
        public void ShouldCopyFilesToCustomInstallationDirectory()
        {
            // Set-up a custom installation directory
            string customInstallDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestInstall");
            fileSystem.EnsureDirectoryExists(customInstallDirectory);
            // Ensure the directory is empty before we start
            fileSystem.PurgeDirectory(customInstallDirectory, DeletionOptions.TryThreeTimes); 
            variables.Set(SpecialVariables.Package.CustomInstallationDirectory, customInstallDirectory );

            result = DeployPackage("Acme.Web");

            // Assert content was copied to custom-installation directory
            Assert.IsTrue(fileSystem.FileExists(Path.Combine(customInstallDirectory, "assets\\styles.css")));
        }

        [Test]
        public void ShouldExecuteFeatureScripts()
        {
            variables.Set(SpecialVariables.Package.EnabledFeatures, "HelloWorld");
            result = DeployPackage("Acme.Web");
            result.AssertOutput("Hello World!");
        }

        [Test]
        public void ShouldModifyIisWebsiteRoot()
        {
            // If the 'UpdateIisWebsite' variable is set, the website root will be updated

            // Create the website
            var originalWebRootPath = Path.Combine(Path.GetTempPath(), "CalamariTestIisSite");
            fileSystem.EnsureDirectoryExists(originalWebRootPath);
            var webServer = WebServerSupport.AutoDetect();
            var siteName = "CalamariTest-" + Guid.NewGuid();
            webServer.CreateWebSiteOrVirtualDirectory(siteName, "/", originalWebRootPath, 1081);

            variables.Set(SpecialVariables.Package.UpdateIisWebsite, true.ToString());
            variables.Set(SpecialVariables.Package.UpdateIisWebsiteName, siteName);

            result = DeployPackage("Acme.Web");

            Assert.AreEqual(
                Path.Combine(stagingDirectory, "Acme.Web\\1.0.0"), 
                webServer.GetHomeDirectory(siteName, "/"));

            // And remove the website
            webServer.DeleteWebSite(siteName);
            fileSystem.DeleteDirectory(originalWebRootPath);
        }

        [Test]
        public void ShouldRunConfiguredScripts()
        {
            Assert.Fail();
        }

        CalamariResult DeployPackage(string packageName)
        {
            using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage(packageName, "1.0.0")))
            {
                variables.Save(variablesFile.FilePath);

                return Invoke(Calamari()
                    .Action("deploy-package")
                    .Argument("package", acmeWeb.FilePath)
                    .Argument("variables", variablesFile.FilePath));
            }
        }

        [TearDown]
        public void CleanUp()
        {
            new CalamariPhysicalFileSystem().PurgeDirectory(stagingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
        }

        private void AssertXmlNodeValue(string xmlFile, string nodeXPath, string value)
        {
            var configXml = new XmlDocument(); 
            configXml.LoadXml( fileSystem.ReadFile(xmlFile));
            var node = configXml.SelectSingleNode(nodeXPath);

            Assert.AreEqual(value, node.Value);
        }
    }
}
