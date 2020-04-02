using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Azure.CloudServices.Commands;

namespace Calamari.Azure.CloudServices
{
    public class AzureCloudServicesExtension : ICalamariExtension
    {
        readonly Dictionary<string, Type> commandTypes = new Dictionary<string, Type>
        {
            {"deploy-azure-cloud-service", typeof(DeployAzureCloudServiceCommand)},
            {"extract-cspkg", typeof(ExtractAzureCloudServicePackageCommand)}
        };
        
        public Dictionary<string, Type> RegisterCommands()
        {
            return commandTypes;
        }

        public void Load(ContainerBuilder builder)
        {
        }
    }
}