using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Extensibility;
using Calamari.Extensibility.Features;
using IPackageExtractor = Calamari.Extensibility.Features.IPackageExtractor;

namespace Calamari.Deployment
{
    [Feature("DeployPackage", "I Am A Run Script")]
    public class DeployPackageFeature : IPackageDeploymentFeature
    {
        public void AfterDeploy(IVariableDictionary variables)
        {

        }

        public void Rollback(IVariableDictionary variables)
        {

        }

        public void Cleanup(IVariableDictionary variables)
        {

        }
    }
}