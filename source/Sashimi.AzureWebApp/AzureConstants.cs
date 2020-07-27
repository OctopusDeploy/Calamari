using System;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.DeploymentTools;

namespace Sashimi.AzureWebApp
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

    class AzureConstants
    {
        public static readonly ActionHandlerCategory AzureActionHandlerCategory = new ActionHandlerCategory("Azure", "Azure", 600);
        public static CalamariFlavour CalamariAzure = new CalamariFlavour("Calamari.AzureWebApp");
    }
}