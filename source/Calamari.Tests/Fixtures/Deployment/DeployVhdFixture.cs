using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using Calamari.Deployment;
using Calamari.Extensibility;
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
        private const string Environment = "Production";

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
        [RequiresWindowsServer2012OrAbove]
        public void ShouldDeployAVhd()
        {
            RunDeployment();
        }


        private void RunDeployment()
        {
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.Vhd";
            Variables[SpecialVariables.Vhd.ApplicationPath] = "ApplicationPath";
            Variables["foo"] = "bar";
            Variables[SpecialVariables.Package.SubstituteInFilesTargets] = "web.config";
            Variables[SpecialVariables.Package.SubstituteInFilesEnabled] = "True";
            Variables[SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles] = "True";
            Variables[SpecialVariables.Environment.Name] = Environment;
            Variables[SpecialVariables.Package.JsonConfigurationVariablesEnabled] = "True";
            Variables[SpecialVariables.Package.JsonConfigurationVariablesTargets] = "appsettings.json";

            using (var vhd = new TemporaryFile(VhdBuilder.BuildSampleVhd(ServiceName)))
            using (var file = new TemporaryFile(PackageBuilder.BuildSimpleZip(ServiceName, "1.0.0", Path.GetDirectoryName(vhd.FilePath))))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0"));
                result.AssertOutput("Extracted 2 files");

                // mounts VHD
                result.AssertOutput($"VHD at {Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");

                // runs predeploy etc
                result.AssertOutput("Bonjour from PreDeploy.ps1");

                // can access mountpoint from predeploy
                result.AssertOutputMatches(@"VHD is mounted at [A-Z]:\\");

                // variable substitution in files
                result.AssertOutputMatches(@"Performing variable substitution on '[A-Z]:\\ApplicationPath\\web\.config'");

                // config transforms
                result.AssertOutputMatches(@"Transforming '[A-Z]:\\ApplicationPath\\web\.config' using '[A-Z]:\\ApplicationPath\\web\.Production\.config'") ;

                // json substitutions
                result.AssertOutputMatches(@"Performing JSON variable replacement on '[A-Z]:\\ApplicationPath\\appsettings\.json'");
            }
        }
    }
}