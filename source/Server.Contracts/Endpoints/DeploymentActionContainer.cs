using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Ocl.Converters;
using Octopus.Server.MessageContracts.Features.Feeds;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Endpoints
{
    public class DeploymentActionContainer
    {
        public const string DefaultTag = "latest";
        public string? Image { get; set; }

        [JsonProperty("FeedId")] // This is named FeedId for backward-compatibility as we don't yet want to change the underlying database JSON/schema.
        [OclName("feed")]
        public FeedIdOrName? FeedIdOrName { get; set; }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(Image) && FeedIdOrName is not null;
        }

        public IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(KnownVariables.Action.Container.Image, Image);
        }
    }
}