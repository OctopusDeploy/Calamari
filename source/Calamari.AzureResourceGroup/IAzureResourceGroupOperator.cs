using System.Threading.Tasks;
using Azure.ResourceManager.Resources.Models;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup
{
    // The single point at which resource-group deployment talks to Azure, so the behaviour's template,
    // parameter, deployment-mode and name logic can be unit-tested with a mock.
    public interface IAzureResourceGroupOperator
    {
        Task Deploy(IAzureAccount account,
                    string subscriptionId,
                    string resourceGroupName,
                    string deploymentName,
                    ArmDeploymentMode deploymentMode,
                    string template,
                    string? parameters,
                    IVariables variables);

        // As Deploy, but creates the resource group in the given location if it does not already exist (Bicep flow).
        Task DeployCreatingResourceGroup(IAzureAccount account,
                                         string subscriptionId,
                                         string resourceGroupName,
                                         string resourceGroupLocation,
                                         string deploymentName,
                                         ArmDeploymentMode deploymentMode,
                                         string template,
                                         string? parameters,
                                         IVariables variables);
    }
}
