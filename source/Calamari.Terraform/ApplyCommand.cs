using System;
using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Newtonsoft.Json.Linq;

namespace Calamari.Terraform
{
    [Command("apply-terraform", Description = "Applies a Terraform template")]
    public class ApplyCommand : TerraformCommand
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public ApplyCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, ISubstituteInFiles substituteInFiles, IExtractPackage extractPackage)
            : base(log, variables, fileSystem, substituteInFiles, extractPackage)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(log, fileSystem, commandLineRunner, deployment, environmentVariables))
            {
                cli.ExecuteCommand("apply", "-no-color", "-auto-approve",
                    cli.TerraformVariableFiles, cli.ActionParams);

                // Attempt to get the outputs. This will fail if none are defined in versions prior to v0.11.8
                // Please note that we really don't want to log the following command output as it can contain sensitive variables etc. hence the IgnoreCommandOutput()
                if (cli.ExecuteCommand(out var result, false, "output", "-no-color", "-json").ExitCode != 0)
                {
                    return;
                }

                foreach (var (name, token) in OutputVariables(result))
                {
                    Boolean.TryParse(token.SelectToken("sensitive")?.ToString(), out var isSensitive);

                    var json = token.ToString();
                    var value = token.SelectToken("value")?.ToString();

                    log.SetOutputVariable($"TerraformJsonOutputs[{name}]", json, deployment.Variables, isSensitive);
                    if (value != null)
                    {
                        log.SetOutputVariable($"TerraformValueOutputs[{name}]", value, deployment.Variables, isSensitive);
                    }

                    log.Info(
                        $"Saving {(isSensitive ? "sensitive " : String.Empty)}variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.TerraformJsonOutputs[\"{name}\"]' with the JSON value only of '{json}'");
                    if (value != null)
                    {
                        log.Info(
                            $"Saving {(isSensitive ? "sensitive " : String.Empty)}variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.TerraformValueOutputs[\"{name}\"]' with the value only of '{value}'");
                    }
                }
            }
        }

        IEnumerable<(string, JToken)> OutputVariables(string result)
        {
            var jObj = JObject.Parse(result);

            foreach (var property in jObj.Properties())
            {
                yield return (property.Name, property.Value);
            }
        }
    }
}