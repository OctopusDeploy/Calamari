using System.ComponentModel.DataAnnotations;
using Octopus.Server.MessageContracts.Attributes;
using Octopus.Server.MessageContracts.Features.DeploymentTargets;
using Octopus.Server.MessageContracts.Features.Machines;

#nullable disable
namespace Sashimi.AzureCloudService.Endpoints
{
    public class CloudServiceEndpointResource : EndpointResource
    {
#pragma warning disable 672
        public override CommunicationStyle CommunicationStyle => CommunicationStyle.AzureCloudService;
#pragma warning restore 672

        [Trim]
        [Writeable]
        [Required(ErrorMessage = "Please specify an account.")]
        public string AccountId { get; set; }

        [Trim]
        [Writeable]
        [Required(ErrorMessage = "Please specify the cloud service name.")]
        public string CloudServiceName { get; set; }

        [Trim]
        [Writeable]
        [Required(ErrorMessage = "Please specify a storage account.")]
        public string StorageAccountName { get; set; }

        [Trim]
        [Writeable]
        public string Slot { get; set; }

        [Writeable]
        public bool SwapIfPossible { get; set; }

        [Writeable]
        public bool UseCurrentInstanceCount { get; set; }

        [Trim]
        [Writeable]
        public string DefaultWorkerPoolId { get; set; }
    }
}