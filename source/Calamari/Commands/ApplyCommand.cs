using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Terraform.Behaviours;

namespace Calamari.Terraform.Commands
{
    [Command("apply-terraform", Description = "Applies a Terraform template")]
    public class ApplyCommand : TerraformCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<ApplyBehaviour>();
        }
    }
}