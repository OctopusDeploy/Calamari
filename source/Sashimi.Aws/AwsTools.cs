using Sashimi.Server.Contracts.DeploymentTools;

namespace Sashimi.Aws
{
    public static class AwsTools
    {
        public static IDeploymentTool AwsCli = new InPathDeploymentTool("Octopus.Dependencies.AWSCLI32", "AWSCLI");
        public static IDeploymentTool AwsPowershell = new BoostrapperModuleDeploymentTool("Octopus.Dependencies.AWSPS", new[] { "AWSPS\\AWSPowerShell.psd1" });
    }
}