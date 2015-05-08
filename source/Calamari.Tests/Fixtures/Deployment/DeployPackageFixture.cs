using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Iis;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
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
        string customDirectory;

        [SetUp]
        public void SetUp()
        {
            fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            // Ensure staging directory exists and is empty 
            stagingDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestStaging");
            customDirectory = Path.Combine(Path.GetTempPath(), "CalamariTestCustom");
            fileSystem.EnsureDirectoryExists(stagingDirectory);
            fileSystem.PurgeDirectory(stagingDirectory, DeletionOptions.TryThreeTimes);

            Environment.SetEnvironmentVariable("TentacleJournal", Path.Combine(stagingDirectory, "DeploymentJournal.xml" ));

            variables = new VariableDictionary();
            variables.EnrichWithEnvironmentVariables();
            variables.Set(SpecialVariables.Tentacle.Agent.ApplicationDirectoryPath, stagingDirectory);
            variables.Set("PreDeployGreeting", "Bonjour");
        }

        [Test]
        public void ShouldDeployPackage()
        {
            var result = DeployPackage("Acme.Web");
            result.AssertZero();

            result.AssertOutput("Extracting package to: " + Path.Combine(stagingDirectory, "Acme.Web", "1.0.0"));

            if (CalamariEnvironment.IsRunningOnNix)
            {
                result.AssertOutput("Extracted 9 files");
                result.AssertOutput("Bonjour from PreDeploy.sh");
            }
            else
            {
                result.AssertOutput("Extracted 8 files");
                result.AssertOutput("Bonjour from PreDeploy.ps1");
            }
        }

        [Test]
        public void ShouldSetExtractionVariable()
        {
            var result = DeployPackage("Acme.Web");
            result.AssertZero();
            result.AssertOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, Is.EqualTo(Path.Combine(stagingDirectory, "Acme.Web", "1.0.0")));
        }

        [Test]
        public void ShouldCopyToCustomDirectoryExtractionVariable()
        {
            variables[SpecialVariables.Package.CustomInstallationDirectory] = customDirectory;
            var result = DeployPackage("Acme.Web");
            result.AssertZero();
            result.AssertOutput("Copying package contents to");
            result.AssertOutputVariable(SpecialVariables.Package.Output.InstallationDirectoryPath, Is.EqualTo(customDirectory));
        }

        [Test]
        public void ShouldSubstituteVariablesInFiles()
        {
            variables.Set("foo", "bar");
            // Enable file substitution and configure the target
            variables.Set(SpecialVariables.Package.SubstituteInFilesEnabled, true.ToString());
            variables.Set(SpecialVariables.Package.SubstituteInFilesTargets, "web.config");

            var result = DeployPackage("Acme.Web");

            // The #{foo} variable in web.config should have been replaced by 'bar'
            AssertXmlNodeValue(Path.Combine(stagingDirectory, "Acme.Web", "1.0.0", "web.config"), "configuration/appSettings/add[@key='foo']/@value", "bar");
        }

        [Test]
        [Category(TestEnvironment.CompatableOS.Windows)] //Problem with XML on Linux
        public void ShouldTransformConfig()
        {
            // Set the environment, and the flag to automatically run config transforms
            variables.Set(SpecialVariables.Environment.Name, "Production");
            variables.Set(SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles, true.ToString());

            var result = DeployPackage("Acme.Web");

            // The environment app-setting value should have been transformed to 'Production'
            AssertXmlNodeValue(Path.Combine(stagingDirectory, "Production", "Acme.Web", "1.0.0", "web.config"), "configuration/appSettings/add[@key='environment']/@value", "Production");
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

            var result = DeployPackage("Acme.Web");

            // Assert content was copied to custom-installation directory
            Assert.IsTrue(fileSystem.FileExists(Path.Combine(customInstallDirectory, "assets", "styles.css")));
        }

        [Test]
        public void ShouldExecuteFeatureScripts()
        {
            variables.Set(SpecialVariables.Package.EnabledFeatures, "Octopus.Features.HelloWorld");
            var result = DeployPackage("Acme.Web");
            result.AssertOutput("Hello World!");
        }

        [Test]
        [Category(TestEnvironment.CompatableOS.Windows)]
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

            var result = DeployPackage("Acme.Web");

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
            variables.Set(SpecialVariables.Package.EnabledFeatures, SpecialVariables.Features.CustomScripts);

            if (CalamariEnvironment.IsRunningOnNix)
            {
                variables.Set(ConfiguredScriptConvention.GetScriptName(DeploymentStages.Deploy, "sh"), "echo 'The wheels on the bus go round...'");
            }
            else
            {
                variables.Set(ConfiguredScriptConvention.GetScriptName(DeploymentStages.Deploy, "ps1"), "Write-Host 'The wheels on the bus go round...'");
            }

            var result = DeployPackage("Acme.Web");

            result.AssertOutput("The wheels on the bus go round...");
        }

        [Test]
        public void ShouldAddJournalEntry()
        {
            var result = DeployPackage("Acme.Web");

            result.AssertOutput("Adding journal entry");
        }

        [Test]
        public void ShouldLogVariables()
        {
            variables.Set(SpecialVariables.PrintVariables, true.ToString());
            variables.Set(SpecialVariables.PrintEvaluatedVariables, true.ToString());
            variables.Set(SpecialVariables.Environment.Name, "Production");
            const string variableName = "foo";
            const string rawVariableValue = "The environment is #{Octopus.Environment.Name}";
            variables.Set(variableName, rawVariableValue) ;

            var result = DeployPackage("Acme.Web");

            //Assert raw variables were output
            result.AssertOutput("The following variables are available:");
            result.AssertOutput(string.Format("[{0}] = '{1}'", variableName, rawVariableValue));

            //Assert evaluated variables were output
            result.AssertOutput("The following evaluated variables are available:");
            result.AssertOutput(string.Format("[{0}] = '{1}'", variableName, "The environment is Production"));
        }

        [Test]
        public void ShouldSkipIfAlreadyInstalled()
        {
            variables.Set(SpecialVariables.Package.SkipIfAlreadyInstalled, true.ToString());
            variables.Set(SpecialVariables.RetentionPolicySet, "a/b/c/d");
            variables.Set(SpecialVariables.Package.NuGetPackageId, "Acme.Web");
            variables.Set(SpecialVariables.Package.NuGetPackageVersion, "1.0.0");

            var result = DeployPackage("Acme.Web");
            result.AssertZero();
            result.AssertOutput("The package has been installed to");

            result = DeployPackage("Acme.Web");
            result.AssertZero();
            result.AssertOutput("The package has already been installed on this machine");
        }

        [Test]
        [Category(TestEnvironment.CompatableOS.Windows)] // Re-enable when deployments enabled again.
        public void ShouldDeployInParallel()
        {
            var extractionDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var errors = new List<Exception>();

            using (var acmeWeb = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.Web", "1.0.0")))
            {
                var threads = Enumerable.Range(0, 4).Select(i => new Thread(new ThreadStart(delegate
                {
                    try
                    {
                        CalamariResult result;
                        using (var variablesFile = new TemporaryFile(Path.GetTempFileName()))
                        {
                            variables.Save(variablesFile.FilePath);

                            result = Invoke(Calamari()
                                .Action("deploy-package")
                                .Argument("package", acmeWeb.FilePath)
                                .Argument("variables", variablesFile.FilePath));
                        }

                        result.AssertZero();
                        var extracted = result.GetOutputForLineContaining("Extracting package to: ");
                        result.AssertOutput(CalamariEnvironment.IsRunningOnNix
                            ? "Extracted 9 files"
                            : "Extracted 8 files");

                        lock (extractionDirectories)
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
            }

            var allErrors = string.Join(Environment.NewLine, errors.Select(e => e.ToString()));
            Assert.That(allErrors, Is.EqualTo(""), allErrors);
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
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(stagingDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
            CalamariPhysicalFileSystem.GetPhysicalFileSystem().PurgeDirectory(customDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
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
