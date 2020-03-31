using Calamari.Commands.Support;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("destroyplan-terraform", Description = "Plans the destruction of Terraform resources")]
    public class DestroyPlanCommand : PlanCommand
    {
        public DestroyPlanCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner) 
            : base(log, variables, fileSystem, commandLineRunner)
        {
        }

        protected override string ExtraParameter => "-destroy";
    }
}