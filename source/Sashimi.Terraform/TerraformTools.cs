using Sashimi.Server.Contracts.DeploymentTools;

namespace Sashimi.Terraform
{
    class TerraformTools
    {
        public static IDeploymentTool TerraformCli = new InPathDeploymentTool(
            "Octopus.Dependencies.TerraformCLI",
            "contentFiles\\any\\win",
            TerraformSpecialVariables.Calamari.TerraformCliPath
        );
    }
}