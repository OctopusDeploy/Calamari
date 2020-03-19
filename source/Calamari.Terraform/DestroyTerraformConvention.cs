using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    public class DestroyTerraformConvention : TerraformConvention
    {
        readonly ICommandLineRunner commandLineRunner;

        public DestroyTerraformConvention(ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner) : base(fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCLIExecutor(fileSystem, commandLineRunner, deployment, environmentVariables))
            {
                cli.ExecuteCommand("destroy", "-force", "-no-color", cli.TerraformVariableFiles, cli.ActionParams)
                    .VerifySuccess();
            }
        }
    }
}