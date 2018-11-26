using System;
using System.Collections.Specialized;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public class PlanTerraformConvention : TerraformConvention
    {
        private readonly string extraParameter;

        public PlanTerraformConvention(ICalamariFileSystem fileSystem, IFileSubstituter fileSubstituter, string extraParameter = "") : base(fileSystem, fileSubstituter)
        {
            this.extraParameter = extraParameter;
        }

        protected override void Execute(RunningDeployment deployment, StringDictionary environmentVariables)
        {
            string results;
            using (var cli = new TerraformCLIExecutor(fileSystem, deployment))
            {
                var resultCode = cli.ExecuteCommand(environmentVariables, out results, out var commandResult, "plan", "-no-color", "-detailed-exitcode", extraParameter, cli.TerraformVariableFiles, cli.ActionParams);

                if (resultCode == 1)
                {
                    commandResult.VerifySuccess();
                }

                Log.Info(
                    $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode}' with the detailed exit code of the plan, with value '{resultCode}'.");
                Log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, resultCode.ToString());
            }

            Log.Info(
                $"Saving variable 'Octopus.Action[\"{deployment.Variables["Octopus.Action.StepName"]}\"].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            Log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results);
        }
    }
}