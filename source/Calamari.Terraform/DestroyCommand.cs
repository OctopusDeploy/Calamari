using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("destroy-terraform", Description = "Destroys Terraform resources")]
    public class DestroyCommand : TerraformCommand
    {
        public DestroyCommand(IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, IConventionFactory conventionFactory)
            : base(variables, fileSystem, conventionFactory, new DestroyTerraformConvention(fileSystem, commandLineRunner))
        {
        }
    }
}