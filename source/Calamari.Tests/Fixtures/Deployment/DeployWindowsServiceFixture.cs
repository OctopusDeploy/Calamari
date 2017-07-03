using System.IO;
using Calamari.Deployment;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class DeployWindowsServiceFixture : DeployWindowsServiceAbstractFixture
    {
        protected override string ServiceName => "Acme.Service";
        
        [Test]
        public void ShouldDeployAndInstallASimpleService()
        {
            RunDeployment();
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreArguments()
        {
            Variables[SpecialVariables.Action.WindowsService.Arguments] = "--SomeArg";
            RunDeployment();
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreSpacesInArguments()
        {
            Variables[SpecialVariables.Action.WindowsService.Arguments] = "\"Argument with Space\" ArgumentWithoutSpace";
            RunDeployment();
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreSpacesInThePath()
        {
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.CustomDirectory,Octopus.Features.WindowsService";
            var installDir = Path.Combine(CustomDirectory, "A Directory With A Space In It");
            Variables[SpecialVariables.Package.CustomInstallationDirectory] = installDir;

            RunDeployment();

            Assert.IsTrue(File.Exists(Path.Combine(installDir, $"{ServiceName}.exe")), "Installed in the right location");
        }

        [Test]
        public void ShouldDeployAndInstallWhenThereAreSpacesInThePathAndArguments()
        {
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.CustomDirectory,Octopus.Features.WindowsService";
            var installDir = Path.Combine(CustomDirectory, "A Directory With A Space In It");
            Variables[SpecialVariables.Package.CustomInstallationDirectory] = installDir;
            Variables[SpecialVariables.Action.WindowsService.Arguments] = "\"Argument with Space\" ArgumentWithoutSpace";

            RunDeployment();

            Assert.IsTrue(File.Exists(Path.Combine(installDir, $"{ServiceName}.exe")), "Installed in the right location");
        }

    }
}