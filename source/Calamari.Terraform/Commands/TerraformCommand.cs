using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Terraform.Behaviours;

namespace Calamari.Terraform.Commands
{
    public abstract class TerraformCommand : PipelineCommand
    {
        protected override bool IncludeConfiguredScriptBehaviour => false;
        protected override bool IncludePackagedScriptBehaviour => false;

        protected override IEnumerable<IPreDeployBehaviour> PreDeploy(PreDeployResolver resolver)
        {
            yield return resolver.Create<TerraformSubstituteBehaviour>();
        }
    }
}