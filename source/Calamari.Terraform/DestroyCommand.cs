using Calamari.Commands.Support;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        public DestroyCommand(): base((fileSystem, substituter) =>  new DestroyTerraformConvention(fileSystem, substituter))
        {
        }
    }
}