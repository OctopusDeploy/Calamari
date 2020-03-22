using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    public class DestroyPlanTerraformConvention : PlanTerraformConvention
    {
        public DestroyPlanTerraformConvention(ILog log, ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner) : base(log, fileSystem, commandLineRunner, "-destroy")
        {
        }
    }
}