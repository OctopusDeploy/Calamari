using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    public class PlanTerraformConvention : TerraformConvention
    {
        private readonly string extraParameter;

        public PlanTerraformConvention(ICalamariFileSystem fileSystem, string extraParameter = "") : base(fileSystem)
        {
            this.extraParameter = extraParameter;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCLIExecutor(fileSystem, deployment, environmentVariables))
            {
                var resultCode = cli.ExecuteCommand(out results, out var commandResult, "plan", "-no-color", "-detailed-exitcode", extraParameter, cli.TerraformVariableFiles, cli.ActionParams);

                if (resultCode == 1)
                {
                    commandResult.VerifySuccess();
                }

                Log.Info(
                    $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode}' with the detailed exit code of the plan, with value '{resultCode}'.");
                Log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, resultCode.ToString(), deployment.Variables);
            }

            Log.Info(
                $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            Log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results, deployment.Variables);
        }
    }
}