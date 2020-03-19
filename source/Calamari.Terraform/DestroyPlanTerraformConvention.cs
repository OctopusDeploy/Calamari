using Calamari.Integration.FileSystem;

namespace Calamari.Terraform
{
    public class DestroyPlanTerraformConvention : PlanTerraformConvention
    {
        public DestroyPlanTerraformConvention(ILog log, ICalamariFileSystem fileSystem) : base(log, fileSystem, "-destroy")
        {
        }
    }
}