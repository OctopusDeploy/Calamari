using Octopus.Server.MessageContracts.Attributes;
using Octopus.Server.MessageContracts.Features.DeploymentTargets;
using Octopus.Server.MessageContracts.Features.Machines;

namespace Sashimi.AzureAppService.Endpoints
{
    public class AzureWebAppEndpointResource : EndpointResource
    {
#pragma warning disable 672
        public override CommunicationStyle CommunicationStyle => CommunicationStyle.AzureWebApp;
#pragma warning restore 672

        [Trim]
        [Writeable]
        public string AccountId { get; set; } = string.Empty;

        [Trim]
        [Writeable]
        public string ResourceGroupName { get; set; } = string.Empty;

        [Trim]
        [Writeable]
        public string WebAppName { get; set; } = string.Empty;

        [Trim]
        [Writeable]
        public string? WebAppSlotName { get; set; }

        [Trim]
        [Writeable]
        public string? DefaultWorkerPoolId { get; set; }
    }
}