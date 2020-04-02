using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        readonly TerraformCliExecutor.Factory terraformCliExecutorFactory;

        public DestroyCommand(ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage,
            TerraformCliExecutor.Factory terraformCliExecutorFactory)
            : base(log, variables, fileSystem, substituteInFiles, extractPackage)
        {
            this.terraformCliExecutorFactory = terraformCliExecutorFactory;
        }
        
        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            var cli = terraformCliExecutorFactory(deployment, environmentVariables);
            
            cli.ExecuteCommand("destroy", "-force", "-no-color", cli.TerraformVariableFiles, cli.ActionParams)
                .VerifySuccess();
        }
    }
}