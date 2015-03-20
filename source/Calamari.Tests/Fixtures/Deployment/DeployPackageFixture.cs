using System;
using System.IO;
using Calamari.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployPackageFixture : CalamariFixture
    {
        string installDirectory;
        VariableDictionary variables;

        [SetUp]
        public void SetUp()
        {
            installDirectory = Path.Combine(Path.GetTempPath(), "TestInstalls");
            new CalamariPhysicalFileSystem().EnsureDirectoryExists(installDirectory);

            variables = new VariableDictionary();
            variables.Set("Octopus.Tentacle.Agent.ApplicationDirectoryPath", installDirectory);
        }

        [Test]
        public void ShouldDeployPackage()
        {
            var result = DeployPackage("Acme.Web");
            
            result.AssertZero();

            result.AssertOutput("Extracting package to: " + installDirectory + "\\Acme.Web\\1.0.0");
            result.AssertOutput("Extracted 4 files");
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
            new CalamariPhysicalFileSystem().PurgeDirectory(installDirectory, DeletionOptions.TryThreeTimesIgnoreFailure);
        }
    }
}
