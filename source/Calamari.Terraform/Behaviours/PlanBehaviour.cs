using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Terraform.Helpers;
using Newtonsoft.Json.Linq;
using NuGet.Versioning;

namespace Calamari.Terraform.Behaviours
{
    public class PlanBehaviour : TerraformDeployBehaviour
    {
        public const string LineEndingRE = "\r\n?|\n";
        public const string TerraformPlanJsonMinVersion = "0.12";
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public PlanBehaviour(ILog log,
                             ICalamariFileSystem fileSystem,
                             ICommandLineRunner commandLineRunner) : base(log)
        {
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected virtual string ExtraParameter => "";

        bool IsUsingPlanJSON(RunningDeployment deployment, Version version)
        {
            return !version.IsLessThan(TerraformPlanJsonMinVersion) &&
                   deployment.Variables.GetFlag(TerraformSpecialVariables.Action.Terraform.PlanJsonOutput);
        }

        public string GetOutputParameter(RunningDeployment deployment, Version version)
        {
            return IsUsingPlanJSON(deployment, version) ? "--json" : "";
        }

        protected override Task Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCliExecutor(log,
                                                      fileSystem,
                                                      commandLineRunner,
                                                      deployment,
                                                      environmentVariables))
            {
                if (cli.Version.IsLessThan(TerraformPlanJsonMinVersion) && deployment.Variables.GetFlag(TerraformSpecialVariables.Action.Terraform.PlanJsonOutput))
                {
                    log.Warn($"JSON output is not supported in versions of Terraform prior to {TerraformPlanJsonMinVersion}. The version of Terraform being used is {cli.Version}");
                }

                var commandResult = cli.ExecuteCommand(out results,
                                                       "plan",
                                                       "-no-color",
                                                       "-detailed-exitcode",
                                                       GetOutputParameter(deployment, cli.Version),
                                                       ExtraParameter,
                                                       cli.TerraformVariableFiles,
                                                       cli.ActionParams);
                var resultCode = commandResult.ExitCode;

                cli.VerifySuccess(commandResult, r => r.ExitCode == 0 || r.ExitCode == 2);

                log.Info(
                         $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode}' with the detailed exit code of the plan, with value '{resultCode}'.");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, resultCode.ToString(), deployment.Variables);

                if (IsUsingPlanJSON(deployment, cli.Version))
                {
                    CaptureJsonOutput(deployment, results);
                }
                else
                {
                    CapturePlainTextOutput(deployment, results);
                }
            }

            return Task.CompletedTask;
        }

        void CapturePlainTextOutput(RunningDeployment deployment, string results)
        {
            log.Info(
                     $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results, deployment.Variables);
        }

        public void CaptureJsonOutput(RunningDeployment deployment, string results)
        {
            var lines = Regex.Split(results, LineEndingRE);
            for (var index = 0; index < lines.Length; ++index)
            {
                var variableName = $"TerraformPlanLine[{index}].JSON";

                log.Info(
                         $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{variableName}' with the details of the plan");
                log.SetOutputVariable(variableName, lines[index], deployment.Variables);

                CaptureChangeSummary(deployment, lines[index]);
            }
        }

        void CaptureChangeSummary(RunningDeployment deployment, string line)
        {
            try
            {
                var parsed = JObject.Parse(line);
                if (parsed["type"].ToString() != "change_summary")
                    return;

                log.Info(
                         $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanJsonChangesAdd}' with the the number of added resources in the plan");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanJsonChangesAdd, parsed["changes"]?["add"]?.ToString() ?? "", deployment.Variables);

                log.Info(
                         $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanJsonChangesRemove}' with the the number of removed resources in the plan");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanJsonChangesRemove, parsed["changes"]?["remove"]?.ToString() ?? "", deployment.Variables);

                log.Info(
                         $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanJsonChangesChange}' with the the number of changed resources in the plan");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanJsonChangesChange, parsed["changes"]?["change"]?.ToString() ?? "", deployment.Variables);
            }
            catch
            {
                log.Warn("Terraform output invalid JSON in the line: " + line);
            }
        }
    }
}