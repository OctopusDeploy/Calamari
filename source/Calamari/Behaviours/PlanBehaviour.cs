using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Terraform.Behaviours
{
    class PlanBehaviour : TerraformDeployBehaviour
    {
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

        protected override Task Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            string results;
            using (var cli = new TerraformCliExecutor(log,
                                                      fileSystem,
                                                      commandLineRunner,
                                                      deployment,
                                                      environmentVariables))
            {
                var commandResult = cli.ExecuteCommand(out results,
                                                       "plan",
                                                       "-no-color",
                                                       "-detailed-exitcode",
                                                       ExtraParameter,
                                                       cli.TerraformVariableFiles,
                                                       cli.ActionParams);
                var resultCode = commandResult.ExitCode;

                cli.VerifySuccess(commandResult, r => r.ExitCode == 0 || r.ExitCode == 2);

                log.Info(
                         $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode}' with the detailed exit code of the plan, with value '{resultCode}'.");
                log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanDetailedExitCode, resultCode.ToString(), deployment.Variables);
            }

            log.Info(
                     $"Saving variable 'Octopus.Action[{deployment.Variables["Octopus.Action.StepName"]}].Output.{TerraformSpecialVariables.Action.Terraform.PlanOutput}' with the details of the plan");
            log.SetOutputVariable(TerraformSpecialVariables.Action.Terraform.PlanOutput, results, deployment.Variables);

            return this.CompletedTask();
        }
    }
}