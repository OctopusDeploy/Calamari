using Calamari.Shared;
using Calamari.Shared.Convention;
using Calamari.Shared.Features;

namespace Calamari.Features.Conventions
{
    [ConventionMetadata(CommonConventions.PackageExtraction, "Extracts the package", true)]
    public class PackageExtractionConvention : IInstallConvention
    {


        public void Install(IVariableDictionary variables)
        {
            
        }
    }
    
}
