using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        readonly ICommandLineRunner commandLineRunner;

        public DestroyCommand(IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
            : base(variables, fileSystem)
        {
            this.commandLineRunner = commandLineRunner;
        }
        
        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(fileSystem, commandLineRunner, deployment, environmentVariables))
            {
                cli.ExecuteCommand("destroy", "-force", "-no-color", cli.TerraformVariableFiles, cli.ActionParams)
                    .VerifySuccess();
            }
        }
    }
}