using System;
using System.IO;
using System.Text;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Terraform;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Terraform
{
    [TestFixture]
    public class TerraformFixture
    {
        [Test]
        public void ExtraInitParametersAreSet()
        {
            ExecuteAndReturnLogOutput(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, "-upgrade"), "Simple")
                .Should().Contain("init -no-color -get-plugins=true -upgrade");
        }

        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            ExecuteAndReturnLogOutput(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, false.ToString()), "Simple")
                .Should().Contain("init -no-color -get-plugins=false");
        }

        [Test]
        public void AttachLogFile()
        {
            ExecuteAndReturnLogOutput(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AttachLogFile, true.ToString()), "Simple")
                .Should().Contain("##octopus[createArtifact ");
        }

        [Test]
        public void AdditionalActionParams()
        {
            ExecuteAndReturnLogOutput(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\"");
                }, "Simple")
                .Should().Contain("plan -no-color  -var my_var=\"Hello world\"");
        }

        [Test]
        public void VarFiles()
        {
            ExecuteAndReturnLogOutput(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                }, "WithVariables")
                .Should().Contain("plan -no-color -var-file=\"example.tfvars\"");
        }

        [Test]
        public void SubtituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                }, "WithVariables")
                .Should().Contain("plan -no-color -var-file=\"example.tfvars\"");
        }

        string ExecuteAndReturnLogOutput(Action<VariableDictionary> populateVariables, string folderName)
        {
            using (var currentDirectory = TemporaryDirectory.Create())
            {
                var variablesFile = Path.GetTempFileName();
                var variables = new VariableDictionary();
                variables.Set(TerraformSpecialVariables.Calamari.TerraformCliPath, TestEnvironment.GetTestPath("Terraform"));
                variables.Set(SpecialVariables.OriginalPackageDirectoryPath, currentDirectory.DirectoryPath);
                variables.Set(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                    TestEnvironment.GetTestPath("Terraform", "contentFiles", "any", "win", "terraform.exe"));

                populateVariables(variables);

                var terraformFiles = TestEnvironment.GetTestPath("Terraform", folderName);
                foreach (var file in Directory.EnumerateFiles(terraformFiles))
                {
                    File.Copy(file, Path.Combine(currentDirectory.DirectoryPath, Path.GetFileName(file)));
                }

                variables.Save(variablesFile);

                var sb = new StringBuilder();
                Log.StdOut = new IndentedTextWriter(new StringWriter(sb));

                using (new TemporaryFile(variablesFile))
                {
                    var command = new PlanCommand();
                    var result = command.Execute(new[] {"--variables", $"{variablesFile}",});

                    result.Should().Be(0);
                }

                return sb.ToString();
            }
        }

        [Test]
        public void AWS()
        {
            using (var currentDirectory = TemporaryDirectory.Create())
            {
                var variablesFile = Path.GetTempFileName();
                var variables = new VariableDictionary();
                variables.Set("Octopus.Action.AwsAccount.Variable", "AWSAccount");
                variables.Set("AWSAccount.AccessKey", Environment.GetEnvironmentVariable("AWS_Calamari_Access"));
                variables.Set("AWSAccount.SecretKey", Environment.GetEnvironmentVariable("AWS_Calamari_Secret"));
                variables.Set(TerraformSpecialVariables.Calamari.TerraformCliPath, TestEnvironment.GetTestPath("Terraform"));
                variables.Set(SpecialVariables.OriginalPackageDirectoryPath, currentDirectory.DirectoryPath);
                variables.Set(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                    TestEnvironment.GetTestPath("Terraform", "contentFiles", "any", "win", "terraform.exe"));
                variables.Set(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\"");
                variables.Set("Octopus.Action.StepName", "Step Name");

                var terraformFiles = TestEnvironment.GetTestPath("Terraform", "Example1");
                foreach (var file in Directory.EnumerateFiles(terraformFiles))
                {
                    File.Copy(file, Path.Combine(currentDirectory.DirectoryPath, Path.GetFileName(file)));
                }

                variables.Save(variablesFile);

                using (new TemporaryFile(variablesFile))
                {
                    var command = new PlanCommand();
                    var result = command.Execute(new[] {"--variables", $"{variablesFile}",});

                    result.Should().Be(0);
                }
            }
        }

        [Test]
        public void Apply()
        {
            using (var currentDirectory = TemporaryDirectory.Create())
            {
                var variablesFile = Path.GetTempFileName();
                var variables = new VariableDictionary();
                variables.Set(TerraformSpecialVariables.Calamari.TerraformCliPath, TestEnvironment.GetTestPath("Terraform"));
                variables.Set(SpecialVariables.OriginalPackageDirectoryPath, currentDirectory.DirectoryPath);
                variables.Set(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                    TestEnvironment.GetTestPath("Terraform", "contentFiles", "any", "win", "terraform.exe"));
                variables.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                variables.Set("Should_Be_Substituted", "Hello World");
                variables.Set("Octopus.Action.StepName", "Step Name");

                var terraformFiles = TestEnvironment.GetTestPath("Terraform", "Example1");
                foreach (var file in Directory.EnumerateFiles(terraformFiles))
                {
                    File.Copy(file, Path.Combine(currentDirectory.DirectoryPath, Path.GetFileName(file)));
                }

                variables.Save(variablesFile);

                using (new TemporaryFile(variablesFile))
                {
                    var command = new ApplyCommand();
                    var result = command.Execute(new[] {"--variables", $"{variablesFile}",});

                    result.Should().Be(0);
                }
            }
        }

        [Test]
        public void PlanBackup()
        {
            ExecuteAndReturnLogOutput(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                }, "ExtraInitParametersAreSet")
                .Should().Contain("##octopus[createArtifact ");

            using (var currentDirectory = TemporaryDirectory.Create())
            {
                var variablesFile = Path.GetTempFileName();
                var variables = new VariableDictionary();
                variables.Set(TerraformSpecialVariables.Calamari.TerraformCliPath, TestEnvironment.GetTestPath("Terraform"));
                variables.Set(SpecialVariables.OriginalPackageDirectoryPath, currentDirectory.DirectoryPath);
                variables.Set(TerraformSpecialVariables.Action.Terraform.CustomTerraformExecutable,
                    TestEnvironment.GetTestPath("Terraform", "contentFiles", "any", "win", "terraform.exe"));
                variables.Set(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\"");
                variables.Set("Octopus.Action.StepName", "Step Name");

                var terraformFiles = TestEnvironment.GetTestPath("Terraform", "Example1");
                foreach (var file in Directory.EnumerateFiles(terraformFiles))
                {
                    File.Copy(file, Path.Combine(currentDirectory.DirectoryPath, Path.GetFileName(file)));
                }

                variables.Save(variablesFile);

                using (new TemporaryFile(variablesFile))
                {
                    var command = new PlanCommand();
                    var result = command.Execute(new[] {"--variables", $"{variablesFile}",});

                    result.Should().Be(0);
                }
            }
        }
    }
}
