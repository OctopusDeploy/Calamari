using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Fixtures.Deployment.Packages;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    public abstract class DeployWindowsServiceAbstractFixture : DeployPackageFixture
    {
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

        protected virtual string PackageName => "Acme.Service";

        protected abstract string ServiceName { get; }

        private void DeleteExistingService()
        {
            var service = GetInstalledService();
            if (service != null)
            {
                var system32 = Environment.GetFolderPath(Environment.SpecialFolder.System);
                var sc = Path.Combine(system32, "sc.exe");

                Process.Start(new ProcessStartInfo(sc, $"stop {ServiceName}") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
                Process.Start(new ProcessStartInfo(sc, $"delete {ServiceName}") { CreateNoWindow = true, UseShellExecute = false })?.WaitForExit();
            }
        }

        protected void RunDeployment(Action extraAsserts = null)
        {
            if (string.IsNullOrEmpty(Variables[KnownVariables.Package.EnabledFeatures]))
                Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.WindowsService";
            Variables["Octopus.Action.WindowsService.CreateOrUpdateService"] = "True";
            Variables["Octopus.Action.WindowsService.ServiceAccount"] = "_CUSTOM";
            Variables["Octopus.Action.WindowsService.StartMode"] = "auto";
            Variables["Octopus.Action.WindowsService.ServiceName"] = ServiceName;
            if (Variables["Octopus.Action.WindowsService.DisplayName"] == null)
            {
                Variables["Octopus.Action.WindowsService.DisplayName"] = ServiceName;
            }
            Variables["Octopus.Action.WindowsService.ExecutablePath"] = $"{PackageName}.exe";

            using (var file = new TemporaryFile(PackageBuilder.BuildSamplePackage(PackageName, "1.0.0")))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, PackageName, "1.0.0"));

                result.AssertOutput("Extracted 1 files");

                using (var installedService = GetInstalledService())
                {
                    Assert.NotNull(installedService, "Service is installed");
                    Assert.AreEqual(ServiceControllerStatus.Running, installedService.Status);
                }

                extraAsserts?.Invoke();
            }
        }

        private ServiceController GetInstalledService()
        {
            return ServiceController.GetServices().FirstOrDefault(s => s.ServiceName == ServiceName);
        }
    }
}