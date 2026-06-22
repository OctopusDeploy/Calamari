using System.Threading.Tasks;
using Azure.ResourceManager.Resources.Models;
using Calamari.CloudAccounts;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureResourceGroup
{
    /// <summary>
    /// The single point at which resource-group deployment talks to Azure: creates the ArmClient, submits the
    /// ARM deployment, polls and finalises it. Mocking this lets the template-source resolution, parameter
    /// normalisation and deployment-mode/name logic in the deploy behaviours be unit-tested without a real
    /// Azure connection.
    /// </summary>
    interface IAzureResourceGroupOperator
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
