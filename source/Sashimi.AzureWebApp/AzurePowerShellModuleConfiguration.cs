using System;
using Octopus.Configuration;

namespace Sashimi.AzureWebApp
{
    class AzurePowerShellModuleConfiguration
    {
        readonly IKeyValueStore settings;
        const string Key = "Octopus.Azure.PowerShellModule";

        public AzurePowerShellModuleConfiguration(IKeyValueStore settings)
        {
            this.settings = settings;
        }

        public string AzurePowerShellModule
        {
            get { return settings.Get<string>(Key); }
            set { settings.Set(Key, value); }
        }
    }
}