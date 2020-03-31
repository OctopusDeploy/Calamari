using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    [Command("destroyplan-terraform", Description = "Plans the destruction of Terraform resources")]
    public class DestroyPlanCommand : PlanCommand
    {
        public DestroyPlanCommand(ILog log, IVariables variables, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner, ISubstituteInFiles substituteInFiles) 
            : base(log, variables, fileSystem, commandLineRunner, substituteInFiles)
        {
        }

        protected override string ExtraParameter => "-destroy";
    }
}