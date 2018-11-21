using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("apply-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class ApplyCommand : TerraformCommand
    {
        public ApplyCommand(): base((fileSystem, substituter) =>  new ApplyTerraformConvention(fileSystem, substituter))
        {
        }
    }
}