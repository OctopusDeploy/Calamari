using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("apply-terraform", Description = "Applies a Terraform template")]
    public class ApplyCommand : TerraformCommand
    {
        public ApplyCommand(): base(fileSystem =>  new ApplyTerraformConvention(fileSystem))
        {
        }
    }
}