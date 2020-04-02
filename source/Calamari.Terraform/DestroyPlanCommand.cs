using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Packages;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    class DestroyPlanCommand : PlanCommand
    {
        public DestroyPlanCommand(
            ILog log, 
            IVariables variables, 
            ICalamariFileSystem fileSystem, 
            ICommandLineRunner commandLineRunner, 
            ISubstituteInFiles substituteInFiles,
            IExtractPackage extractPackage) 
            : base(log, variables, fileSystem, commandLineRunner, substituteInFiles, extractPackage)
        {
        }

        protected override string ExtraParameter => "-destroy";
    }
}