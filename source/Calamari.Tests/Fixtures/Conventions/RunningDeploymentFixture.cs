using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Calamari.Variables;
using NUnit.Framework;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class RunningDeploymentFixture
    {
        [Test]
        public void ShouldReturnCorrectDirectories()
        {
            var deployment = new RunningDeployment("C:\\Package.nupkg", new CalamariVariables());

            // When no custom installation directory is chosen, custom points to staging
            deployment.Variables.Set(Common.Variables.SpecialVariables.OriginalPackageDirectoryPath, "C:\\Apps\\MyPackage\\1.0.0_1");
            Assert.That(deployment.StagingDirectory, Is.EqualTo("C:\\Apps\\MyPackage\\1.0.0_1"));
            Assert.That(deployment.CustomDirectory, Is.EqualTo("C:\\Apps\\MyPackage\\1.0.0_1"));
            Assert.That(deployment.CurrentDirectory, Is.EqualTo("C:\\Apps\\MyPackage\\1.0.0_1"));

            // Custom installation directory is always available, but the staging directory points to current
            deployment.Variables.Set(PackageVariables.CustomInstallationDirectory, "C:\\MyWebsite");
            Assert.That(deployment.StagingDirectory, Is.EqualTo("C:\\Apps\\MyPackage\\1.0.0_1"));
            Assert.That(deployment.CustomDirectory, Is.EqualTo("C:\\MyWebsite"));
            Assert.That(deployment.CurrentDirectory, Is.EqualTo("C:\\Apps\\MyPackage\\1.0.0_1"));

            // After the package contents is copied to the custom installation directory, the current directory changes
            deployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.CustomDirectory;
            Assert.That(deployment.StagingDirectory, Is.EqualTo("C:\\Apps\\MyPackage\\1.0.0_1"));
            Assert.That(deployment.CustomDirectory, Is.EqualTo("C:\\MyWebsite"));
            Assert.That(deployment.CurrentDirectory, Is.EqualTo("C:\\MyWebsite"));
        }
    }
}
