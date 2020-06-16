using System;
using System.Collections.Generic;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Endpoints
{
    public class DeploymentActionContainer
    {
        public const string DefaultTag = "latest";
        public string? Image { get; set; }
        public string? FeedId { get; set; }

        public bool IsConfigured()
        {
            return !string.IsNullOrEmpty(Image) && !string.IsNullOrEmpty(FeedId);
        }

        public IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(KnownVariables.Action.Container.Image, Image);
        }
    }
}