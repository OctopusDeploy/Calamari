using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    public class DestroyPlanTerraformConvention : PlanTerraformConvention
    {
        public DestroyPlanTerraformConvention(ICalamariFileSystem fileSystem, ILog log) : base(fileSystem, log, "-destroy")
        {
        }
    }
}