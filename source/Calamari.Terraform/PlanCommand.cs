using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    [Command("plan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class PlanCommand : TerraformCommand
    {
        public PlanCommand(IVariables variables, ICalamariFileSystem fileSystem, ILog log): base(variables, fileSystem, new PlanTerraformConvention(fileSystem, log))
        {
        }
    }
}
