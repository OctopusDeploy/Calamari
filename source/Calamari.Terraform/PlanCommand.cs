using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("plan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class PlanCommand : TerraformCommand
    {
        readonly ICommandLineRunner commandLineRunner;

        public PlanCommand(IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
            : base(variables, fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
        }
        
        protected virtual string ExtraParameter => "";
        
        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCliExecutor(fileSystem, commandLineRunner, deployment, environmentVariables))
            {
                var commandResult = cli.ExecuteCommand(out results, "plan", "-no-color", "-detailed-exitcode", ExtraParameter, cli.TerraformVariableFiles, cli.ActionParams);
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