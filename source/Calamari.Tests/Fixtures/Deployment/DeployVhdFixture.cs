using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Tests.Fixtures.Deployment.Packages;
using Calamari.Tests.Helpers;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.Deployment
{
    [TestFixture]
    [Category(TestCategory.CompatibleOS.OnlyWindows)]
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
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.Vhd,Octopus.Features.ConfigurationTransforms";
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
                result.AssertOutput($"VHD partition 0 from {Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");

                // runs predeploy etc
                result.AssertOutput("Bonjour from PreDeploy.ps1");

                // can access mountpoint from predeploy
                result.AssertOutputMatches(@"VHD is mounted at [A-Z]:\\");

                // variable substitution in files
                result.AssertOutputMatches(@"Performing variable substitution on '[A-Z]:\\ApplicationPath\\web\.config'");

                // config transforms
                result.AssertOutputMatches(@"Transforming '[A-Z]:\\ApplicationPath\\web\.config' using '[A-Z]:\\ApplicationPath\\web\.Production\.config'");

                // json substitutions
                result.AssertOutputMatches(@"Performing JSON variable replacement on '[A-Z]:\\ApplicationPath\\appsettings\.json'");
            }
        }

        [Test]
        [RequiresAdmin]
        [RequiresWindowsServer2012OrAbove]        
        public void ShouldDeployAVhdWithTwoPartitions()
        {
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.Vhd,Octopus.Features.ConfigurationTransforms";
            Variables[SpecialVariables.Vhd.ApplicationPath] = "ApplicationPath";
            Variables["foo"] = "bar";
            Variables[SpecialVariables.Package.SubstituteInFilesTargets] = "web.config";
            Variables[SpecialVariables.Package.SubstituteInFilesEnabled] = "True";
            Variables[SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles] = "True";
            Variables[SpecialVariables.Environment.Name] = Environment;
            Variables[SpecialVariables.Package.JsonConfigurationVariablesEnabled] = "True";
            Variables[SpecialVariables.Package.JsonConfigurationVariablesTargets] = "appsettings.json";

            Variables["OctopusVhdPartitions[1].ApplicationPath"] = "PathThatDoesNotExist";

            using (var vhd = new TemporaryFile(VhdBuilder.BuildSampleVhd(ServiceName, twoPartitions: true)))
            using (var file = new TemporaryFile(PackageBuilder.BuildSimpleZip(ServiceName, "1.0.0", Path.GetDirectoryName(vhd.FilePath))))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0"));
                result.AssertOutput("Extracted 2 files");

                // mounts VHD
                result.AssertOutput($"VHD partition 0 from {Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");
                result.AssertOutput($"VHD partition 1 from {Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");

                // handles additionalpaths setting not being valid for all partitions
                result.AssertOutputMatches(@"[A-Z]:\\PathThatDoesNotExist not found so not added to Calamari processing paths");

                // runs predeploy etc
                result.AssertOutput("Bonjour from PreDeploy.ps1");

                // can access mountpoint from predeploy
                result.AssertOutputMatches(@"VHD is mounted at [A-Z]:\\");
                result.AssertOutputMatches(@"VHD partition 0 is mounted at [A-Z]:\\");
                result.AssertOutputMatches(@"VHD partition 1 is mounted at [A-Z]:\\");

                // variable substitution in files
                result.AssertOutputMatches(@"Performing variable substitution on '[A-Z]:\\ApplicationPath\\web\.config'");

                // config transforms
                result.AssertOutputMatches(@"Transforming '[A-Z]:\\ApplicationPath\\web\.config' using '[A-Z]:\\ApplicationPath\\web\.Production\.config'");

                // json substitutions
                result.AssertOutputMatches(@"Performing JSON variable replacement on '[A-Z]:\\ApplicationPath\\appsettings\.json'");
            }
        }

        [Test]
        [RequiresAdmin]
        [RequiresWindowsServer2012OrAbove]
        public void ShouldBlockMountAndOverrideAppPath()
        {
            Variables[SpecialVariables.Package.EnabledFeatures] = "Octopus.Features.Vhd,Octopus.Features.ConfigurationTransforms";
            Variables[SpecialVariables.Vhd.ApplicationPath] = "ApplicationPath";
            Variables["foo"] = "bar";
            Variables[SpecialVariables.Package.SubstituteInFilesTargets] = "web.config";
            Variables[SpecialVariables.Package.SubstituteInFilesEnabled] = "True";
            Variables[SpecialVariables.Package.AutomaticallyRunConfigurationTransformationFiles] = "True";
            Variables[SpecialVariables.Environment.Name] = Environment;
            Variables[SpecialVariables.Package.JsonConfigurationVariablesEnabled] = "True";
            Variables[SpecialVariables.Package.JsonConfigurationVariablesTargets] = "appsettings.json";

            Variables["OctopusVhdPartitions[0].Mount"] = "false";
            Variables["OctopusVhdPartitions[1].ApplicationPath"] = "AlternateApplicationPath";

            using (var vhd = new TemporaryFile(VhdBuilder.BuildSampleVhd(ServiceName, twoPartitions: true)))
            using (var file = new TemporaryFile(PackageBuilder.BuildSimpleZip(ServiceName, "1.0.0", Path.GetDirectoryName(vhd.FilePath))))
            {
                var result = DeployPackage(file.FilePath);
                result.AssertSuccess();

                result.AssertOutput("Extracting package to: " + Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0"));
                result.AssertOutput("Extracted 2 files");

                // mounts VHD
                result.AssertNoOutput($"VHD partition 0 from {Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");
                result.AssertOutput($"VHD partition 1 from {Path.Combine(StagingDirectory, Environment, ServiceName, "1.0.0", ServiceName + ".vhdx")} mounted to");

                // runs predeploy etc
                result.AssertOutput("Bonjour from PreDeploy.ps1");

                // can access mountpoint from predeploy
                result.AssertOutputMatches(@"VHD is mounted at [A-Z]:\\");
                result.AssertNoOutputMatches(@"VHD partition 0 is mounted at [A-Z]:\\");
                result.AssertOutputMatches(@"VHD partition 1 is mounted at [A-Z]:\\");

                // variable substitution in files
                result.AssertOutputMatches(@"Performing variable substitution on '[A-Z]:\\AlternateApplicationPath\\web\.config'");

                // config transforms
                result.AssertOutputMatches(@"Transforming '[A-Z]:\\AlternateApplicationPath\\web\.config' using '[A-Z]:\\AlternateApplicationPath\\web\.Production\.config'");

                // json substitutions
                result.AssertOutputMatches(@"Performing JSON variable replacement on '[A-Z]:\\AlternateApplicationPath\\appsettings\.json'");
            }
        }
    }
}