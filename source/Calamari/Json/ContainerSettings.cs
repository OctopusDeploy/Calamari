using System;
using System.Collections.Generic;
using System.Text;

namespace Calamari.AzureAppService.Json
{
    public class ContainerSettings
    {
        public bool IsEnabled { get; set; }

        public string RegistryUrl { get; set; } //feed Id

        public string ImageName { get; set; } //Package Id

        public string ImageTag { get; set; }
    }
}
