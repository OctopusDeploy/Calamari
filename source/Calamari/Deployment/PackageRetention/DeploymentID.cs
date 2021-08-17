using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.PackageRetention
{
    public class DeploymentID : CaseInsensitiveTinyType
    {
        public DeploymentID(string value) : base(value)
        {
        }

        public DeploymentID(IVariables variables) : base(variables.Get(KnownVariables.Deployment.Id))
        {
            //TODO: do we need validation on the deployment id here?
        }
    }
}