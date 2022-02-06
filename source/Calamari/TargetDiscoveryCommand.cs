using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Calamari.Azure;
////using Calamari.AzureAppService.Azure;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;

namespace Calamari.AzureAppService
{
    [Command("target-discovery", Description = "Discover Azure web applications")]
    public class TargetDiscoveryCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<TargetDiscoveryBehaviour>();
        }
    }
}