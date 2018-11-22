using System.Collections.Specialized;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public class DestroyTerraformConvention : TerraformConvention
    {
        public DestroyTerraformConvention(ICalamariFileSystem fileSystem, IFileSubstituter fileSubstituter) : base(fileSystem, fileSubstituter)
        {
        }

        protected override void Execute(RunningDeployment deployment, StringDictionary environmentVariables)
        {
            using (var cli = new TerraformCLIExecutor(fileSystem, deployment))
            {
                cli.ExecuteCommand($"destroy -force -no-color {cli.TerraformVariableFiles} {cli.ActionParams}", environmentVariables);
            }
        }
    }
}