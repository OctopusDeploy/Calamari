using System;
using System.Collections.Generic;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    public class DestroyTerraformConvention : TerraformConvention
    {
        public DestroyTerraformConvention(ILog log, ICalamariFileSystem fileSystem) : base(log, fileSystem)
        {
        }

        protected override void Execute(RunningDeployment deployment, Dictionary<string, string> environmentVariables)
        {
            using (var cli = new TerraformCliExecutor(Log, fileSystem, deployment, environmentVariables))
            {
                cli.ExecuteCommand("destroy", "-force", "-no-color", cli.TerraformVariableFiles,
                    cli.ActionParams);
            }
        }
    }
}