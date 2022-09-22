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
    class DestroyBehaviour : TerraformDeployBehaviour
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public DestroyBehaviour(ILog log,
                                ICalamariFileSystem fileSystem,
                                ICommandLineRunner commandLineRunner) : base(log)
        {
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected override Task Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(log,
                                                      fileSystem,
                                                      commandLineRunner,
                                                      deployment,
                                                      environmentVariables))
            {
                cli.ExecuteCommand("destroy",
                                   "-auto-approve",
                                   "-no-color",
                                   cli.TerraformVariableFiles,
                                   cli.ActionParams)
                   .VerifySuccess();
            }

            return this.CompletedTask();
        }
    }
}