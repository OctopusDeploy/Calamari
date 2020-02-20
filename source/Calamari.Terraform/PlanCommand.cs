using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("plan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class PlanCommand : TerraformCommand
    {
        public PlanCommand(CalamariVariableDictionary variables, ICalamariFileSystem fileSystem): base(variables, fileSystem, new PlanTerraformConvention(fileSystem))
        {
        }
    }
}
