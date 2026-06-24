using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Terraform.Commands;
using Calamari.Terraform.Tests.CommonTemplates;
using Calamari.Testing;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Terraform.Tests.ExternalToolIntegration
{
    // Integration tests that drive the real terraform CLI against local templates (no cloud provider).
    // The real-cloud variants live in TerraformCloudCommandsFixture.
    [TestFixture("0.13.7")]
    [TestFixture("1.8.5")]
    public class CommandsFixture : TerraformCommandsFixtureBase
    {
        public CommandsFixture(string version) : base(version)
        {
        }

        [Test]
        public void OverridingCacheFolder_WithNonsense_ThrowsAnError()
        {
            ExecuteAndReturnLogOutput("apply-terraform",
                                      _ =>
                                      {
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Package);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                                                          JsonConvert.SerializeObject(new Dictionary<string, string> { { "TF_PLUGIN_CACHE_DIR", "Nonsense" } }));
                                      },
                                      "Simple")
                .Should()
                .ContainAll("The specified plugin cache dir", "cannot be opened");
        }

        [Test]
        public void NotProvidingEnvVariables_DoesNotCrashEverything()
        {
            ExecuteAndReturnLogOutput("apply-terraform",
                                      _ =>
                                      {
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Package);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables, null);
                                      },
                                      "Simple")
                .Should()
                .NotContain("Error");
        }

        [Test]
        public void UserDefinedEnvVariables_OverrideDefaultBehaviour()
        {
            string template = TemplateLoader.LoadTextTemplate("SingleVariable.json");

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "{}");
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.EnvironmentVariables,
                                                          JsonConvert.SerializeObject(new Dictionary<string, string> { { "TF_VAR_ami", "new ami value" } }));
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[ami]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[ami]"].Value.Should().Be("new ami value");
                                      });
        }

        [Test]
        public void ExtraInitParametersAreSet()
        {
            IgnoreIfVersionIsNotInRange("0.0.0", "1.0.0", "-get-plugins was removed in 0.15.0/1.0.0");
            var additionalParams = "-var-file=\"backend.tfvars\"";
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalInitParams, additionalParams),
                                      "Simple")
                .Should()
                .Contain($"init -get-plugins=true {additionalParams}");
        }

        [Test]
        public void AllowPluginDownloadsShouldBeDisabled()
        {
            IgnoreIfVersionIsNotInRange("0.0.0", "0.15.0", "-get-plugins was removed in 0.15.0/1.0.0");
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AllowPluginDownloads,
                                                          false.ToString());
                                      },
                                      "Simple")
                .Should()
                .Contain("init -get-plugins=false");
        }

        [Test]
        public void AttachLogFile()
        {
            ExecuteAndReturnLogOutput(planCommand,
                                      _ =>
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AttachLogFile, true.ToString()),
                                      "Simple",
                                      result =>
                                      {
                                          result.Artifacts.Count.Should().Be(1);
                                      });
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -detailed-exitcode -var my_var=\"Hello world\"")]
        [TestCase(typeof(ApplyCommand), "apply -auto-approve -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -detailed-exitcode -destroy -var my_var=\"Hello world\"")]
        [TestCase(typeof(DestroyCommand), "destroy -auto-approve -var my_var=\"Hello world\"")]
        public void AdditionalActionParams(Type commandType, string expected)
        {
            var command = GetCommandFromType(commandType);

            ExecuteAndReturnLogOutput(command,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams, "-var my_var=\"Hello world\"");
                                      },
                                      "AdditionalParams")
                .Should()
                .Contain(expected);
        }

        [Test]
        [TestCase(typeof(PlanCommand), "plan -detailed-exitcode -var-file=\"example.tfvars\"")]
        [TestCase(typeof(ApplyCommand), "apply -auto-approve -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyPlanCommand), "plan -detailed-exitcode -destroy -var-file=\"example.tfvars\"")]
        [TestCase(typeof(DestroyCommand), "destroy -auto-approve -var-file=\"example.tfvars\"")]
        public void VarFiles(Type commandType, string actual)
        {
            ExecuteAndReturnLogOutput(GetCommandFromType(commandType), _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.tfvars"); }, "WithVariables")
                .Should()
                .Contain(actual);
        }

        [Test]
        public void WithOutputSensitiveVariables()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ => { },
                                      "WithOutputSensitiveVariables",
                                      result =>
                                      {
                                          result.OutputVariables.Values.Should().OnlyContain(variable => variable.IsSensitive);
                                      });
        }

        [Test]
        public void OutputAndSubstituteOctopusVariables()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.VarFiles, "example.txt");
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "example.txt");
                                          _.Variables.Add("Octopus.Action.StepName", "Step Name");
                                          _.Variables.Add("Should_Be_Substituted", "Hello World");
                                          _.Variables.Add("Should_Be_Substituted_in_txt", "Hello World from text");
                                      },
                                      "WithVariablesSubstitution",
                                      result =>
                                      {
                                          result.OutputVariables
                                                .ContainsKey("TerraformValueOutputs[my_output]")
                                                .Should()
                                                .BeTrue();
                                          result.OutputVariables["TerraformValueOutputs[my_output]"]
                                                .Value
                                                .Should()
                                                .Be("Hello World");
                                          result.OutputVariables
                                                .ContainsKey("TerraformValueOutputs[my_output_from_txt_file]")
                                                .Should()
                                                .BeTrue();
                                          result.OutputVariables["TerraformValueOutputs[my_output_from_txt_file]"]
                                                .Value
                                                .Should()
                                                .Be("Hello World from text");
                                      });
        }

        [Test]
        public void EnableNoMatchWarningIsNotSet()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { }, "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }

        [Test]
        public void EnableNoMatchWarningIsNotSetWithAdditionSubstitution()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                                      },
                                      "Simple")
                .Should()
                .MatchRegex("No files were found in (.*) that match the substitution target pattern '\\*\\*/\\*\\.tfvars\\.json'")
                .And
                .MatchRegex("No files were found in (.*) that match the substitution target pattern 'doesNotExist.txt'");
        }

        [Test]
        public void EnableNoMatchWarningIsTrue()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                                          _.Variables.Add("Octopus.Action.SubstituteInFiles.EnableNoMatchWarning", "true");
                                      },
                                      "Simple")
                .Should()
                .MatchRegex("No files were found in (.*) that match the substitution target pattern '\\*\\*/\\*\\.tfvars\\.json'")
                .And
                .MatchRegex("No files were found in (.*) that match the substitution target pattern 'doesNotExist.txt'");
        }

        [Test]
        public void EnableNoMatchWarningIsFalse()
        {
            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.FileSubstitution, "doesNotExist.txt");
                                          _.Variables.Add("Octopus.Action.SubstituteInFiles.EnableNoMatchWarning", "False");
                                      },
                                      "Simple")
                .Should()
                .NotContain("No files were found that match the substitution target pattern");
        }

        [Test]
        [TestCase(typeof(PlanCommand))]
        [TestCase(typeof(DestroyPlanCommand))]
        public void TerraformPlanOutput(Type commandType)
        {
            ExecuteAndReturnLogOutput(GetCommandFromType(commandType),
                                      _ => { _.Variables.Add("Octopus.Action.StepName", "Step Name"); },
                                      "Simple",
                                      result =>
                                      {
                                          result.OutputVariables
                                                .ContainsKey("TerraformPlanOutput")
                                                .Should()
                                                .BeTrue();
                                      });
        }

        [Test]
        public void UsesWorkSpace()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Workspace, "myspace"); }, "Simple")
                .Should()
                .Contain("workspace new \"myspace\"");
        }

        [Test]
        public void UsesTemplateDirectory()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateDirectory, "SubFolder"); }, "TemplateDirectory")
                .Should()
                .Contain($"SubFolder{Path.DirectorySeparatorChar}example.tf");
        }

        [Test]
        public async System.Threading.Tasks.Task PlanDetailedExitCode()
        {
            using var stateFileFolder = TemporaryDirectory.Create();

            var output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("2");

            output = await ExecuteAndReturnResult(applyCommand, PopulateVariables, "PlanDetailedExitCode");
            output.FullLog.Should()
                  .Contain("apply -auto-approve");

            output = await ExecuteAndReturnResult(planCommand, PopulateVariables, "PlanDetailedExitCode");
            output.OutputVariables.ContainsKey("TerraformPlanDetailedExitCode").Should().BeTrue();
            output.OutputVariables["TerraformPlanDetailedExitCode"].Value.Should().Be("0");
            return;

            void PopulateVariables(CommandTestBuilderContext _)
            {
                _.Variables.Add(TerraformSpecialVariables.Action.Terraform.AdditionalActionParams,
                                $"-state=\"{Path.Combine(stateFileFolder.DirectoryPath, "terraform.tfstate")}\" -refresh=false");
            }
        }

        [Test]
        public void InlineHclTemplateAndVariables()
        {
            const string variables = "stringvar = \"default string\"";
            string template = TemplateLoader.LoadTextTemplate("HclWithVariables.hcl");

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add("RandomNumber", new Random().Next().ToString());
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, variables);
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[nestedlist]").Should().BeTrue();
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[nestedmap]").Should().BeTrue();
                                      });
        }

        [Test]
        public void InlineHclTemplateWithMultilineOutput()
        {
            const string expected = @"apiVersion: v1
kind: ConfigMap
metadata:
  name: aws-auth
  namespace: kube-system
data:
  mapRoles: |
    - rolearn: arbitrary text
      username: system:node:username
      groups:
        - system:bootstrappers
        - system:nodes";
            string template = $@"locals {{
  config-map-aws-auth = <<CONFIGMAPAWSAUTH
{expected}
CONFIGMAPAWSAUTH
}}

output ""config-map-aws-auth"" {{
    value = ""${{local.config-map-aws-auth}}""
}}";

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, "");
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[config-map-aws-auth]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[config-map-aws-auth]"]
                                           .Value?.TrimEnd()
                                           .Replace("\r\n", "\n")
                                           .Should()
                                           .Be($"{expected.Replace("\r\n", "\n")}");
                                      });
        }

        [Test]
        public void CanDetermineTerraformVersion()
        {
            ExecuteAndReturnLogOutput(applyCommand, _ => { _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Workspace, "testversionspace"); }, "Simple")
                .Should()
                .NotContain("Could not parse Terraform CLI version");
        }

        [Test]
        public void InlineJsonTemplateAndVariables()
        {
            const string variables =
                "{\"ami\":\"new ami value\"}";
            string template = TemplateLoader.LoadTextTemplate("InlineJsonWithVariables.json");

            var randomNumber = new Random().Next().ToString();

            ExecuteAndReturnLogOutput(applyCommand,
                                      _ =>
                                      {
                                          _.Variables.Add("RandomNumber", randomNumber);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.Template, template);
                                          _.Variables.Add(TerraformSpecialVariables.Action.Terraform.TemplateParameters, variables);
                                          _.Variables.Add(ScriptVariables.ScriptSource,
                                                          ScriptVariables.ScriptSourceOptions.Inline);
                                      },
                                      String.Empty,
                                      _ =>
                                      {
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[ami]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[ami]"].Value.Should().Be("new ami value");
                                          _.OutputVariables.ContainsKey("TerraformValueOutputs[random]").Should().BeTrue();
                                          _.OutputVariables["TerraformValueOutputs[random]"].Value.Should().Be(randomNumber);
                                      });
        }
    }
}
