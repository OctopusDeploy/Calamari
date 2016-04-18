using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class DeployWindowsServiceFixture : DeployPackageFixture
    {
        private const string ServiceName = "Acme.Service";

        [SetUp]
        public override void SetUp()
        {
            DeleteExistingService();
            base.SetUp();
        }


        [TearDown]
        public override void CleanUp()
        {
            DeleteExistingService();
            base.CleanUp();
        }

        private static void DeleteExistingService()
        {
            var service = GetInstalledService();
            if (service != null)
            {
                var system32 = System.Environment.GetFolderPath(System.Environment.SpecialFolder.System);
                var sc = Path.Combine(system32, "sc.exe");

                Process.Start(new ProcessStartInfo(sc, $"stop {ServiceName}") {CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
                Process.Start(new ProcessStartInfo(sc, $"delete {ServiceName}") { CreateNoWindow = true, UseShellExecute = false})?.WaitForExit();
            }
        }


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

        private void RunDeployment()
        {
            if (string.IsNullOrEmpty(Variables[SpecialVariables.Package.EnabledFeatures]))
                Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.WindowsService";
            Variables["Octopus.Action.WindowsService.CreateOrUpdateService"] = "True";
            Variables["Octopus.Action.WindowsService.ServiceAccount"] = "LocalSystem";
            Variables["Octopus.Action.WindowsService.StartMode"] = "auto";
            Variables["Octopus.Action.WindowsService.ServiceName"] = ServiceName;
            Variables["Octopus.Action.WindowsService.DisplayName"] = ServiceName;
            Variables["Octopus.Action.WindowsService.ExecutablePath"] = $"{ServiceName}.exe";

            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(ServiceName, "1.0.0")))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertZero();

                result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, ServiceName, "1.0.0"));

                result.AssertOutput("Extracted 1 files");

                using (var installedService = GetInstalledService())
                {
                    Assert.NotNull(installedService, "Service is installed");
                    Assert.AreEqual(ServiceControllerStatus.Running, installedService.Status);
                }
            }
        }

        private static ServiceController GetInstalledService()
        {
            return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == ServiceName);
        }
    }
}