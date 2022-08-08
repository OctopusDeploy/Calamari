using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureResourceGroup
{
    [Command("deploy-azure-resource-group", Description = "Creates a new Azure Resource Group deployment")]
    public class DeployAzureResourceGroupCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<DeployAzureResourceGroupBehaviour>();
        }
    }
}