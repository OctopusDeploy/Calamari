using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Terraform
{
    public class DestroyPlanTerraformConvention : PlanTerraformConvention
    {
        public DestroyPlanTerraformConvention(ICalamariFileSystem fileSystem, ICommandLineRunner commandLineRunner) : base(fileSystem, commandLineRunner, "-destroy")
        {
        }
    }
}