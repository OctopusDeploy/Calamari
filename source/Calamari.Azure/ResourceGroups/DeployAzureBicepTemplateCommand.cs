using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Azure.ResourceGroups
{
    [Command("deploy-azure-bicep-template", Description = "Deploy a Bicep template to Azure")]
    public class DeployAzureBicepTemplateCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<DeployBicepTemplateBehaviour>();
        }
    }
}