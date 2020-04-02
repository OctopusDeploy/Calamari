using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;
        readonly IEnvironmentVariablesFactory environmentVariablesFactory;

        public DestroyCommand(
            ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage,
            IEnvironmentVariablesFactory environmentVariablesFactory
        )
            : base(log, variables, fileSystem, substituteInFiles, extractPackage)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.environmentVariablesFactory = environmentVariablesFactory;
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(log, fileSystem, commandLineRunner, deployment, environmentVariables, environmentVariablesFactory))
            {
                cli.ExecuteCommand("destroy", "-force", "-no-color", cli.TerraformVariableFiles, cli.ActionParams)
                    .VerifySuccess();
            }
        }
    }
}