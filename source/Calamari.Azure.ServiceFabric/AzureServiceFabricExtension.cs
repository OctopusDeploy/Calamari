using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Azure.ServiceFabric.Commands;

namespace Calamari.Azure.ServiceFabric
{
    public class AzureServiceFabricExtension : ICalamariExtension
    {
        readonly Dictionary<string, Type> commandTypes = new Dictionary<string, Type>
        {
            {"deploy-azure-service-fabric-app", typeof(DeployAzureServiceFabricAppCommand)}
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