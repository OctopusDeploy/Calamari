using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("plan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class PlanCommand : TerraformCommand
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public PlanCommand(ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage)
            : base(log, variables, fileSystem, substituteInFiles, extractPackage)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected virtual string ExtraParameter => "";

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCliExecutor(log, fileSystem, commandLineRunner, deployment, environmentVariables))
            {
                var commandResult = cli.ExecuteCommand(out results, "plan", "-no-color", "-detailed-exitcode", ExtraParameter, cli.TerraformVariableFiles, cli.ActionParams);
                var resultCode = commandResult.ExitCode;

                if (resultCode == 1)
                {
                    commandResult.VerifySuccess();
                }

                log.Info(
                    $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode}' with the detailed exit code of the plan, with value '{resultCode}'.");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, resultCode.ToString(), deployment.Variables);
            }

            log.Info(
                $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results, deployment.Variables);
        }
    }
}