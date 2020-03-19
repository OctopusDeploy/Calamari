using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Util;

namespace Calamari.Terraform
{
    public class PlanTerraformConvention : TerraformConvention
    {
        readonly ICommandLineRunner commandLineRunner;
        private readonly string extraParameter;

        public PlanTerraformConvention(ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, string extraParameter = "") : base(fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
            this.extraParameter = extraParameter;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCLIExecutor(fileSystem, commandLineRunner, deployment, environmentVariables))
            {
                var commandResult = cli.ExecuteCommand(out results, "plan", "-no-color", "-detailed-exitcode", extraParameter, cli.TerraformVariableFiles, cli.ActionParams);
                var resultCode = commandResult.ExitCode;
                
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