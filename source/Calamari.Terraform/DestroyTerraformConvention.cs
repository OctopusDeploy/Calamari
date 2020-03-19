using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    public class DestroyTerraformConvention : TerraformConvention
    {
        public DestroyTerraformConvention(ICalamariFileSystem fileSystem) : base(fileSystem)
        {
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(fileSystem, deployment, environmentVariables))
            {
                cli.ExecuteCommand("destroy", "-force", "-no-color", cli.TerraformVariableFiles,
                    cli.ActionParams);
            }
        }
    }
}