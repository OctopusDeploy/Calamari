using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("destroyplan-terraform", Description = "Plans the destruction of Terraform resources")]
    public class DestroyPlanCommand : TerraformCommand
    {
        public DestroyPlanCommand(IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, IConventionFactory conventionFactory) 
            : base(variables, fileSystem, conventionFactory, new DestroyPlanTerraformConvention(fileSystem, commandLineRunner))
        {
        }
    }
}