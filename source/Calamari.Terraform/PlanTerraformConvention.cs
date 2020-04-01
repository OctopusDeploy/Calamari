using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    public class PlanTerraformConvention : TerraformConvention
    {
        readonly ILog log;
        private readonly string extraParameter;

        public PlanTerraformConvention(ICalamariFileSystem fileSystem, ILog log, string extraParameter = "") : base(fileSystem)
        {
            this.log = log;
            this.extraParameter = extraParameter;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCLIExecutor(fileSystem, deployment, environmentVariables))
            {
                var commandResult = cli.ExecuteCommand(out results, "plan", "-no-color", "-detailed-exitcode", extraParameter, cli.TerraformVariableFiles, cli.ActionParams);
                var resultCode = commandResult.ExitCode;
                
                if (resultCode == 1)
                {
                    commandResult.VerifySuccess();
                }

                log.Info(
                    $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode}' with the detailed exit code of the plan, with value '{resultCode}'.");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, resultCode.ToString(), deployment.Variables);
            }

            log.Info(
                $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results, deployment.Variables);
        }
    }
}