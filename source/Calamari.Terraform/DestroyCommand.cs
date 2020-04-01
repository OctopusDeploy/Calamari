using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        public DestroyCommand(IVariables variables, ICalamariFileSystem fileSystem)
            : base(variables, fileSystem, new DestroyTerraformConvention(fileSystem))
        {
        }
    }
}