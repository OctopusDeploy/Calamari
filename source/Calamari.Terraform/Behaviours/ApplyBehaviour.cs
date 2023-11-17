using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Newtonsoft.Json.Linq;

namespace Calamari.Terraform.Behaviours
{
    class ApplyBehaviour : TerraformDeployBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public ApplyBehaviour(ILog log, ICalamariFileSystem fileSystem,
                              ICommandLineRunner commandLineRunner) : base(log)
        {
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected override Task Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using var cli = new TerraformCliExecutor(log,
                                                     fileSystem,
                                                     commandLineRunner,
                                                     deployment,
                                                     environmentVariables);
            cli.ExecuteCommand("apply",
                               "-no-color",
                               "-auto-approve",
                               cli.TerraformVariableFiles,
                               cli.ActionParams);

            // Attempt to get the outputs. This will fail if none are defined in versions prior to v0.11.15
            // Please note that we really don't want to log the following command output as it can contain sensitive variables etc. hence the IgnoreCommandOutput()
            if (cli.ExecuteCommand(out var result,
                                   false,
                                   "output",
                                   "-no-color",
                                   "-json")
                   .ExitCode
                != 0)
                return Task.CompletedTask;

            foreach (var (name, token) in OutputVariables(result))
            {
                bool.TryParse(token.SelectToken("sensitive")?.ToString(), out var isSensitive);

                var json = token.ToString();
                var value = token.SelectToken("value")?.ToString();

                log.SetOutputVariable($"TerraformJsonOutputs[{name}]", json, deployment.Variables, isSensitive);
                if (value != null)
                    log.SetOutputVariable($"TerraformValueOutputs[{name}]", value, deployment.Variables, isSensitive);

                log.Info(
                         $"Saving {(isSensitive ? "sensitive " : string.Empty)}variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.TerraformJsonOutputs[\"{name}\"]' with the JSON value only of '{json}'");
                if (value != null)
                    log.Info(
                             $"Saving {(isSensitive ? "sensitive " : string.Empty)}variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.TerraformValueOutputs[\"{name}\"]' with the value only of '{value}'");
            }

            return Task.CompletedTask;
        }

        IEnumerable<(string, JToken)> OutputVariables(string result)
        {
            var jObj = JObject.Parse(result);

            foreach (var property in jObj.Properties())
                yield return (property.Name, property.Value);
        }
    }
}