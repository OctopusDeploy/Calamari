using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Sashimi.Azure.Accounts.Web
{
    class AzureEnvironmentsListAction : IAsyncApiAction
    {
        static readonly OctopusJsonRegistration<IReadOnlyCollection<AzureEnvironmentResource>> Result = new OctopusJsonRegistration<IReadOnlyCollection<AzureEnvironmentResource>>();
        static readonly IReadOnlyCollection<AzureEnvironmentResource> EnvironmentResources;

        static AzureEnvironmentsListAction()
        {
            EnvironmentResources = GetEnvironments();
        }

        public Task<IOctoResponseProvider> ExecuteAsync(IOctoRequest request)
        {
            return Task.FromResult(Result.Response(EnvironmentResources));
        }

        static IReadOnlyCollection<AzureEnvironmentResource> GetEnvironments()
        {
            var properties = typeof(AzureEnvironment).GetProperties().Where(x => x.PropertyType == typeof(AzureEnvironment));

            var azureEnvironmentsLookup = properties.ToDictionary(x => x.Name, x => (AzureEnvironment)x.GetValue(null, null)!);

            var azureEnvironmentResources = azureEnvironmentsLookup.Select(x => new AzureEnvironmentResource
            {
                Name = x.Key,
                DisplayName = GetKnownEnvironmentDisplayName(x.Key),
                ManagementEndpoint = x.Value.ManagementEndpoint,
                AuthenticationEndpoint = x.Value.AuthenticationEndpoint,
                GraphEndpoint = x.Value.GraphEndpoint,
                ResourceManagerEndpoint = x.Value.ResourceManagerEndpoint,
                StorageEndpointSuffix = x.Value.StorageEndpointSuffix
            }).ToList();

            FixIncorrectAzureEndpoints(azureEnvironmentResources);

            return azureEnvironmentResources;
        }

        static void FixIncorrectAzureEndpoints(IList<AzureEnvironmentResource> azureEnvironmentResources)
        {
            var defaultCloud = azureEnvironmentResources.FirstOrDefault(x => x.Name == "AzureGlobalCloud");
            if (defaultCloud != null)
            {
                defaultCloud.Name = "AzureCloud";
            }

            foreach (var azureEnvironmentResource in azureEnvironmentResources.Where(x => x.StorageEndpointSuffix.StartsWith(".")))
            {
                azureEnvironmentResource.StorageEndpointSuffix = azureEnvironmentResource.StorageEndpointSuffix.Substring(1);
            }
        }

        static string GetKnownEnvironmentDisplayName(string environmentName)
        {
            switch (environmentName)
            {
                case "AzureGlobalCloud":
                case "AzureCloud":
                    return "Azure Global Cloud (default)";
                case "AzureChinaCloud":
                    return "Azure China Cloud";
                case "AzureGermanCloud":
                    return "Azure German Cloud";
                case "AzureUSGovernment":
                    return "Azure US Government";
                default:
                    //If the environment doesn't fall into a case(most likely a new one added after updating the SDK), set its DisplayName == Name.
                    //This will trigger a unit test failure and force the developer to add a custom DisplayName.
                    return environmentName;
            }
        }
    }

    class AzureEnvironmentResource
    {
        public string Name { get; set; } = null!;
        public string DisplayName { get; set; } = null!;
        public string AuthenticationEndpoint { get; set; } = null!;
        public string ResourceManagerEndpoint { get; set; } = null!;
        public string GraphEndpoint { get; set; } = null!;
        public string ManagementEndpoint { get; set; } = null!;
        public string StorageEndpointSuffix { get; set; } = null!;
    }
}