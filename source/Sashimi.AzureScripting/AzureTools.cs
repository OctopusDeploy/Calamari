using System;
using Sashimi.Server.Contracts.DeploymentTools;

namespace Sashimi.AzureScripting
{
    static class AzureTools
    {
        public static IDeploymentTool AzureCLI = new InPathDeploymentTool("Octopus.Dependencies.AzureCLI", "AzureCLI\\wbin");

        public static IDeploymentTool AzureCmdlets = new BoostrapperModuleDeploymentTool("Octopus.Dependencies.AzureCmdlets",
                                                                                         new[]
                                                                                         {
                                                                                             "Powershell\\Azure.Storage\\4.6.1",
                                                                                             "Powershell\\Azure\\5.3.0",
                                                                                             "Powershell",
                                                                                         });
    }
}