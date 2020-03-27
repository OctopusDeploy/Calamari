using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("plan-terraform", Description = "Plans the creation of a Terraform deployment")]
    public class PlanCommand : TerraformCommand
    {
        public PlanCommand(IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, IConventionFactory conventionFactory)
            : base(variables, fileSystem, conventionFactory, new PlanTerraformConvention(fileSystem, commandLineRunner))
        {
        }
    }
}