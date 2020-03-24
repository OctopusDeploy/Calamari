using System;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    public class DeployBilingualPackageFixture : DeployPackageFixture
    {
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
            nupkgFile = new TemporaryFile(PackageBuilder.BuildSamplePackage("Acme.PackageBilingual", "1.0.0"));
            tarFile = new TemporaryFile(TarGzBuilder.BuildSamplePackage("Acme.PackageBilingual", "1.0.0"));
        }

        [OneTimeTearDown]
        public void Dispose()
        {
            nupkgFile.Dispose();
            tarFile.Dispose();
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyWindows)]
        public void ShouldOnlyRunPowerShellScriptsOnWindows()
        {
            var result = DeployPackage(nupkgFile.FilePath);
            result.AssertSuccess();

            // PreDeploy
            result.AssertOutput("hello from PreDeploy.ps1");
            result.AssertNoOutput("hello from PreDeploy.sh");

            // Deploy
            result.AssertOutput("hello from Deploy.ps1");
            result.AssertNoOutput("hello from Deploy.sh");

            // PostDeploy
            result.AssertOutput("hello from PostDeploy.ps1");
            result.AssertNoOutput("hello from PostDeploy.sh");
        }

        [Test]
        [Category(TestCategory.CompatibleOS.OnlyNixOrMac)]
        public void ShouldOnlyRunBashScriptsOnMacOrNix()
        {
            var result = DeployPackage(tarFile.FilePath);
            result.AssertSuccess();

            // PreDeploy
            result.AssertOutput("hello from PreDeploy.sh");
            result.AssertNoOutput("hello from PreDeploy.ps1");

            // Deploy
            result.AssertOutput("hello from Deploy.sh");
            result.AssertNoOutput("hello from Deploy.ps1");

            // PostDeploy
            result.AssertOutput("hello from PostDeploy.sh");
            result.AssertNoOutput("hello from PostDeploy.ps1");
        }
    }
}
