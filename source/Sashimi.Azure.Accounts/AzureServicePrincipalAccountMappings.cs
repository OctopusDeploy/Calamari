using Octopus.Server.Extensibility.Extensions.Mappings;
using Octopus.Server.Extensibility.HostServices.Mapping;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountMappings : IContributeMappings
    {
        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureServicePrincipalAccountResource, AzureServicePrincipalAccountDetails>();
        }
    }
}