using Sashimi.Server.Contracts;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppEndpoint
    {
        public static readonly DeploymentTargetType AzureWebAppDeploymentTargetType = new DeploymentTargetType("AzureWebApp", "Azure Web Application");
    }
}