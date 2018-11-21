using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("plan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class PlanCommand : TerraformCommand
    {
        public PlanCommand(): base((fileSystem, substituter) =>  new PlanTerraformConvention(fileSystem, substituter))
        {
        }
    }
}
