using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class DestroyCommand : TerraformCommand
    {
        public DestroyCommand(): base((fileSystem, substituter) =>  new DestroyTerraformConvention(fileSystem, substituter))
        {
        }
    }
}