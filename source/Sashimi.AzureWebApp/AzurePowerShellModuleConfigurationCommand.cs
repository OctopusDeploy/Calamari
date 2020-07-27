using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Configuration;

namespace Sashimi.AzureWebApp
{
    class AzurePowerShellModuleConfigurationCommand : IContributeToConfigureCommand
    {
        readonly Lazy<AzurePowerShellModuleConfiguration> azurePowerShellModule;

        public AzurePowerShellModuleConfigurationCommand(Lazy<AzurePowerShellModuleConfiguration> azurePowerShellModule)
        {
            this.azurePowerShellModule = azurePowerShellModule;
        }

        public IEnumerable<ConfigureCommandOption> GetOptions()
        {
            yield return new ConfigureCommandOption("azurePowerShellModule=",
                                                    "Path to Azure PowerShell module to be used",
                                                    v =>
                                                    {
                                                        azurePowerShellModule.Value.AzurePowerShellModule = v;
                                                    });
        }
    }
}