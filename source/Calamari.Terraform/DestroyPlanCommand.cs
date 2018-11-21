using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("destroyplan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class DestroyPlanCommand : TerraformCommand
    {
        public DestroyPlanCommand(): base((fileSystem, substituter) =>  new DestroyPlanTerraformConvention(fileSystem, substituter))
        {
        }
    }
}