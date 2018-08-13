using Calamari.Commands.Support;
using Calamari.Deployment.Conventions;
using Calamari.Shared.Commands;

namespace Calamari.Commands
{
    [DeploymentAction("transfer-package", Description = "Copies a deployment package to a specific directory")]
    public class TransferPackageAction : IDeploymentAction
    {
        public void Build(IDeploymentStrategyBuilder deploymentStrategyBuilder)
        {
            deploymentStrategyBuilder.AddConvention<TransferPackageConvention>();
        }
    }
}