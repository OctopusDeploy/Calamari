using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Azure.WebApps.Commands;

namespace Calamari.Azure.WebApps
{
    public class AzureWebAppsExtension : ICalamariExtension
    {
        readonly Dictionary<string, Type> commandTypes = new Dictionary<string, Type>
        {
            {"deploy-azure-web", typeof(DeployAzureWebCommand)}
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