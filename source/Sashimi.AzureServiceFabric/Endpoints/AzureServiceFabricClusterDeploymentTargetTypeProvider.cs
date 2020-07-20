using System;
using System.Linq;
using System.Collections.Generic;
using FluentValidation;
using Newtonsoft.Json;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureServiceFabric.Endpoints
{
    class AzureServiceFabricClusterDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public DeploymentTargetType DeploymentTargetType =>
            AzureServiceFabricClusterEndpoint.AzureServiceFabricClusterDeploymentTargetType;

        public Type DomainType => typeof(AzureServiceFabricClusterEndpoint);
        public Type ApiType => typeof(ServiceFabricEndpointResource);
        public IValidator Validator => new AzureServiceFabricClusterEndpointValidator();

        public IActionHandler? HealthCheckActionHandlerForTargetType()
        {
            return new AzureServiceFabricAppHealthCheckActionHandler();
        }

        public IEnumerable<AccountType> SupportedAccountTypes
        {
            get { yield break; }
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<ServiceFabricEndpointResource, AzureServiceFabricClusterEndpoint>();
        }

        public IEnumerable<(string key, object value)> GetFeatureUsage(IEndpointMetricContext context)
        {
            var serviceFabricEndpoints = context.GetEndpoints<AzureServiceFabricClusterEndpoint>().ToArray();
            var total = serviceFabricEndpoints.Length;

            yield return ("servicefabricclusters", total);

            if (total > 0)
            {
                yield return ("servicefabricclusterdetail",
                    ConvertObject(GetServiceFabricMetrics(serviceFabricEndpoints)));
            }
        }

        static IList<ServiceFabricDetails> GetServiceFabricMetrics(
            IEnumerable<AzureServiceFabricClusterEndpoint> endpoints)
        {
            bool isAzure(AzureServiceFabricClusterEndpoint e) =>
                e.ConnectionEndpoint != null && e.ConnectionEndpoint.Contains("azure.com");

            bool isOnPrem(AzureServiceFabricClusterEndpoint e) =>
                e.ConnectionEndpoint != null && !e.ConnectionEndpoint.Contains("azure.com");

            return endpoints
                .GroupBy(x => x.SecurityMode)
                .Select(x => new ServiceFabricDetails
                {
                    securitymode = x.Key.ToString(),
                    azure = x.Count(isAzure),
                    onprem = x.Count(isOnPrem)
                })
                .ToList();
        }

        static string ConvertObject(object o)
        {
            return Uri.EscapeDataString(JsonConvert.SerializeObject(o));
        }

        class ServiceFabricDetails
        {
            public string? securitymode { get; set; }
            public int azure { get; set; }
            public int onprem { get; set; }
        }
    }
}