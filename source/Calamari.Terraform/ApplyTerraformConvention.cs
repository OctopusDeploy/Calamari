using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Newtonsoft.Json.Linq;

namespace Calamari.Terraform
{
    public class ApplyTerraformConvention : TerraformConvention
    {
        public ApplyTerraformConvention(ICalamariFileSystem fileSystem) : base(fileSystem)
        {
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            IEnumerable<(string, JToken)> OutputVariables(string result)
            {
                var jObj = JObject.Parse(result);

                foreach (var property in jObj.Properties())
                {
                    yield return (property.Name, property.Value);
                }
            }

            using (var cli = new TerraformCLIExecutor(fileSystem, deployment, environmentVariables))
            {
                cli.ExecuteCommand("apply", "-no-color", "-auto-approve",
                    cli.TerraformVariableFiles, cli.ActionParams);

                // Attempt to get the outputs. This will fail if none are defined in versions prior to v0.11.8
                var exitCode = cli.ExecuteCommand(out var result, "output", "-no-color", "-json");
                if (exitCode != 0)
                {
                    return;
                }

                foreach (var (name, token) in OutputVariables(result))
                {
                    Boolean.TryParse(token.SelectToken("sensitive")?.ToString(), out var isSensitive);

                    var json = token.ToString();
                    var value = token.SelectToken("value")?.ToString();
                    
                    Log.SetOutputVariable($"TerraformJsonOutputs[{name}]", json, deployment.Variables, isSensitive);
                    if (value != null)
                    {
                        Log.SetOutputVariable($"TerraformValueOutputs[{name}]", value, deployment.Variables, isSensitive);
                    }

                    Log.Info(
                        $"Saving {(isSensitive ? "sensitive" : String.Empty)}variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.TerraformJsonOutputs[\"{name}\"]' with the JSON value only of '{json}'");
                    if (value != null)
                    {
                        Log.Info(
                            $"Saving {(isSensitive ? "sensitive" : String.Empty)}variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.TerraformValueOutputs[\"{name}\"]' with the value only of '{value}'");
                    }
                }
            }
        }
    }
}