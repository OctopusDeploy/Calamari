using System;
using Octopus.Server.MessageContracts.Features.Feeds;

namespace Sashimi.Server.Contracts.Endpoints
{
    public class DeploymentActionContainerResource
    {
        public string? Image { get; set; }
        public FeedIdOrName? FeedId { get; set; }
    }
}