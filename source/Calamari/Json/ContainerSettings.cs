using System;
using System.Collections.Generic;
using System.Text;

namespace Calamari.AzureAppService.Json
{
    /// <summary>
    /// Example:
    /// {
    ///    "IsEnabled": true,
    ///    "RegistryUrl": "https://index.docker.io",
    ///    "ImageName": "nginx", //"xtreampb/ubuntu_ssh"
    ///    "ImageTag": "latest"
    /// }
    /// </summary>
    public class ContainerSettings
    {
        public bool IsEnabled { get; set; }

        public string RegistryUrl { get; set; }

        public string ImageName { get; set; }

        public string ImageTag { get; set; }
    }
}
