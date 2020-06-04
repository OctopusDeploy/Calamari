using System;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Octopus.Server.Extensibility.HostServices.Mapping;

namespace Sashimi.AzureCloudService
{
    class AzureSubscriptionMappings : IContributeMappings
    {
        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureSubscriptionAccountResource, AzureSubscriptionDetails>();
        }
    }
}