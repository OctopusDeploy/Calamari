using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
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
            Variables[SpecialVariables.Vhd.ApplicationPath] = "ApplicationPath";
            Variables["foo"] = "bar";
            Variables[PackageVariables.SubstituteInFilesTargets] = "web.config";
            Variables[KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles] = "True";
            Variables[DeploymentEnvironment.Name] = Environment;
            Variables[ActionVariables.StructuredConfigurationVariablesTargets] = "appsettings.json";
            Variables[KnownVariables.Package.EnabledFeatures] = $"{KnownVariables.Features.StructuredConfigurationVariables},{KnownVariables.Features.SubstituteInFiles}, {KnownVariables.Features.ConfigurationTransforms},Octopus.Features.Vhd";

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
                result.AssertOutputMatches("Structured variable replacement succeeded on file [A-Z]:\\\\ApplicationPath\\\\appsettings.json with format Json");
            }
        }

        [Test]
        [RequiresAdmin]
        [RequiresWindowsServer2012OrAbove]
        public void ShouldDeployAVhdWithTwoPartitions()
        {
            Variables[SpecialVariables.Vhd.ApplicationPath] = "ApplicationPath";
            Variables["foo"] = "bar";
            Variables[PackageVariables.SubstituteInFilesTargets] = "web.config";
            Variables[KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles] = "True";
            Variables[DeploymentEnvironment.Name] = Environment;
            Variables[ActionVariables.StructuredConfigurationVariablesTargets] = "appsettings.json";
            Variables[KnownVariables.Package.EnabledFeatures] = $"{KnownVariables.Features.StructuredConfigurationVariables},{KnownVariables.Features.SubstituteInFiles}, {KnownVariables.Features.ConfigurationTransforms},Octopus.Features.Vhd";

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
                result.AssertOutputMatches("Structured variable replacement succeeded on file [A-Z]:\\\\ApplicationPath\\\\appsettings.json with format Json");
            }
        }

        [Test]
        [RequiresAdmin]
        [RequiresWindowsServer2012OrAbove]
        public void ShouldBlockMountAndOverrideAppPath()
        {
            Variables[KnownVariables.Package.EnabledFeatures] = "Octopus.Features.Vhd,Octopus.Features.ConfigurationTransforms";
            Variables[SpecialVariables.Vhd.ApplicationPath] = "ApplicationPath";
            Variables["foo"] = "bar";
            Variables[PackageVariables.SubstituteInFilesTargets] = "web.config";
            Variables[KnownVariables.Package.AutomaticallyRunConfigurationTransformationFiles] = "True";
            Variables[DeploymentEnvironment.Name] = Environment;
            Variables[ActionVariables.StructuredConfigurationVariablesTargets] = "appsettings.json";
            Variables[KnownVariables.Package.EnabledFeatures] = $"{KnownVariables.Features.StructuredConfigurationVariables},{KnownVariables.Features.SubstituteInFiles},{KnownVariables.Features.ConfigurationTransforms},Octopus.Features.Vhd";

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
                result.AssertOutputMatches("Structured variable replacement succeeded on file [A-Z]:\\\\AlternateApplicationPath\\\\appsettings.json with format Json");
            }
        }
    }
}