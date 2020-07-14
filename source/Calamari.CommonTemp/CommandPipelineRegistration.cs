using System.Collections.Generic;
using System.Linq;

namespace Calamari.CommonTemp
{
    public abstract class CommandPipelineRegistration
    {
        public virtual IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
        {
            return Enumerable.Empty<IBeforePackageExtractionBehaviour>();
        }

        public virtual IEnumerable<IAfterPackageExtractionBehaviour> AfterPackageExtraction(AfterPackageExtractionResolver resolver)
        {
            return Enumerable.Empty<IAfterPackageExtractionBehaviour>();
        }

        public virtual IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            return Enumerable.Empty<IPreDeployBehaviour>();
        }

        public abstract IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver);

        public virtual IEnumerable<IPostDeployBehaviour> PostDeploy(PostDeployResolver resolver)
        {
            return Enumerable.Empty<IPostDeployBehaviour>();
        }
    }
}