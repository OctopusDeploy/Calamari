using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureResourceGroup.Bicep;

[Command("deploy-azure-bicep-template", Description = "Deploy a Bicep template to Azure")]
// ReSharper disable once ClassNeverInstantiated.Global
public class DeployAzureBicepTemplateCommand : PipelineCommand
{
    protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
    {
        yield return resolver.Create<DeployBicepTemplateBehaviour>();
    }
}