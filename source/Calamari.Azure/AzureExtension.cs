using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Azure.Commands;

namespace Calamari.Azure
{
    public class AzureExtension : ICalamariExtension
    {
        readonly Dictionary<string, Type> commandTypes = new Dictionary<string, Type>
        {
            {"deploy-azure-resource-group", typeof(DeployAzureResourceGroupCommand)}
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