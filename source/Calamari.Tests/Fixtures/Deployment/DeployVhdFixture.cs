using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using Calamari.Util;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestEnvironment.CompatibleOS.Windows)]
    public class DeployVhdFixture : DeployPackageFixture
    {
        private const string ServiceName = "Acme.Vhd";

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();
        }


        [TearDown]
        public override void CleanUp()
        {
            base.CleanUp();
        }

        [Test]
        [RequiresAdmin]
        public void ShouldDeployAVhd()
        {
            RunDeployment();
        }


        private void RunDeployment()
        {
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.Vhd";
            Variables["Octopus.Action.WindowsService.CreateOrUpdateService"] = "True";

            using (var vhd = new TemporaryFile(VhdBuilder.BuildSampleVhd(ServiceName)))
            using (var file = new TemporaryFile(PackageBuilder.BuildSimpleZip(ServiceName, "1.0.0", Path.GetDirectoryName(vhd.FilePath))))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, ServiceName, "1.0.0"));
                result.AssertOutput("Extracted 2 files");
                result.AssertOutput($"VHD at {Path.Combine(StagingDirectory, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");
            }
        }
    }
}