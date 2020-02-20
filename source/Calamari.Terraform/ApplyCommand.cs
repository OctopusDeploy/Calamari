using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octopus.Versioning.Maven;

namespace Calamari.Terraform
{
    [Command("apply-terraform", Description = "Applies a Terraform template")]
    public class ApplyCommand : TerraformCommand
    {
        public ApplyCommand(IVariables variables, ICalamariFileSystem fileSystem)
            : base(variables, fileSystem, new ApplyTerraformConvention(fileSystem))
        {
        }
    }
}