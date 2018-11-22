using System;
using System.IO;
using System.Text;
using Calamari.Commands.Support;
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
            ExecuteAndReturnLogOutput<PlanCommand>(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, "-upgrade"), "Simple")
                .Should().Contain("init -no-color -get-plugins=true -upgrade");
        }

        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            ExecuteAndReturnLogOutput<PlanCommand>(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads, false.ToString()), "Simple")
                .Should().Contain("init -no-color -get-plugins=false");
        }

        [Test]
        public void AttachLogFile()
        {
            ExecuteAndReturnLogOutput<PlanCommand>(_ =>
                    _.Set(TerraformSpecialVariables.Action.Terraform.AttachLogFile, true.ToString()), "Simple")
                .Should().Contain("##octopus[createArtifact ");
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -no-color -var my_var=\"Hello world\"")]
        [TestCase(typeof(ApplyCommand), "apply -no-color -auto-approve -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -destroy -no-color -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyCommand), "destroy -force -no-color -var my_var=\"Hello world\"")]
        public void AdditionalActionParams(Type commandType, string expected)
        {
            ExecuteAndReturnLogOutput(commandType, _ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\"");
                }, "Simple")
                .Should().Contain(expected);
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -no-color -var-file=\"example.tfvars\"")]
        [TestCase(typeof(ApplyCommand), "apply -no-color -auto-approve -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -destroy -no-color -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyCommand), "destroy -force -no-color -var-file=\"example.tfvars\"")]
        public void VarFiles(Type commandType, string actual)
        {
            ExecuteAndReturnLogOutput(commandType, _ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                }, "WithVariables")
                .Should().Contain(actual);
        }

        [Test]
        public void OutputAndSubstituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars");
                    _.Set("Octopus.Action.StepName", "Step Name");
                    _.Set("Should_Be_Substituted", "Hello World");
                }, "WithVariables")
                .Should().Contain("Octopus.Action[\"Step Name\"].Output.TerraformValueOutputs[\"my_output\"]' with the value only of 'Hello World'");
        }

        [Test]
        [TestCase(typeof(PlanCommand))]
        [TestCase(typeof(DestroyPlanCommand))]
        public void SubstituteOctopusVariables(Type commandType)
        {
            ExecuteAndReturnLogOutput(commandType, _ =>
                {
                    _.Set("Octopus.Action.StepName", "Step Name");
                }, "Simple")
                .Should().Contain("Octopus.Action[\"Step Name\"].Output.TerraformPlanOutput");
        }

        [Test]
        public void UsesWorkSpace()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.Workspace, "myspace");
                }, "Simple")
                .Should().Contain("workspace new \"myspace\"");
        }

        [Test]
        public void UsesTemplateDirectory()
        {
            ExecuteAndReturnLogOutput<ApplyCommand>(_ =>
                {
                    _.Set(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, "SubFolder");
                }, "TemplateDirectory")
                .Should().Contain("SubFolder\\example.tf");
        }

        string ExecuteAndReturnLogOutput(Type commandType, Action<VariableDictionary> populateVariables, string folderName)
        {
            void Copy(string sourcePath, string destinationPath)
            {
                foreach (var dirPath in Directory.EnumerateDirectories(sourcePath, "*", SearchOption.AllDirectories))
                {
                    Directory.CreateDirectory(dirPath.Replace(sourcePath, destinationPath));
                }

                foreach (var newPath in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
                {
                    File.Copy(newPath, newPath.Replace(sourcePath, destinationPath), true);
                }
            }

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

                Copy(terraformFiles, currentDirectory.DirectoryPath);

                variables.Save(variablesFile);

                var sb = new StringBuilder();
                Log.StdOut = new IndentedTextWriter(new StringWriter(sb));

                using (new TemporaryFile(variablesFile))
                {
                    var command = (ICommand)Activator.CreateInstance(commandType);
                    var result = command.Execute(new[] {"--variables", $"{variablesFile}",});

                    result.Should().Be(0);
                }

                return sb.ToString();
            }
        }

        string ExecuteAndReturnLogOutput<T>(Action<VariableDictionary> populateVariables, string folderName) where T : ICommand, new()
        {
            return ExecuteAndReturnLogOutput(typeof(T), populateVariables, folderName);
        }
    }
}
