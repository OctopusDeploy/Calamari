using Calamari.Integration.FileSystem;
using Calamari.Integration.Substitutions;

namespace Calamari.Terraform
{
    public class DestroyPlanTerraformConvention : PlanTerraformConvention
    {
        public DestroyPlanTerraformConvention(ICalamariFileSystem fileSystem, IFileSubstituter fileSubstituter) : base(fileSystem, fileSubstituter, "-destroy")
        {
        }
    }
}