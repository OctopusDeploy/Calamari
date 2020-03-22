using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octopus.Versioning.Maven;

namespace Calamari.Terraform
{
    [Command("apply-terraform", Description = "Applies a Terraform template")]
    public class ApplyCommand : TerraformCommand
    {
        public ApplyCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner)
            : base(log, variables, fileSystem, new ApplyTerraformConvention(log, fileSystem, commandLineRunner))
        {
        }
    }
}