using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("destroyplan-terraform", Description = "Plans the destruction of Terraform resources")]
    public class DestroyPlanCommand : TerraformCommand
    {
        public DestroyPlanCommand(): base(fileSystem =>  new DestroyPlanTerraformConvention(fileSystem))
        {
        }
    }
}