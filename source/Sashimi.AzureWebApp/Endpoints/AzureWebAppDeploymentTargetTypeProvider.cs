using System;
using System.Linq;
using System.Collections.Generic;
using FluentValidation;
using Octopus.Server.Extensibility.HostServices.Mapping;
using Sashimi.Azure.Accounts;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureWebApp.Endpoints
{
    class AzureWebAppDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public DeploymentTargetType DeploymentTargetType => AzureWebAppEndpoint.AzureWebAppDeploymentTargetType;
        public Type DomainType => typeof(AzureWebAppEndpoint);
        public Type ApiType => typeof(AzureWebAppEndpointResource);
        public IValidator Validator => new AzureWebAppEndpointValidator();

        public IEnumerable<AccountType> SupportedAccountTypes
        {
            get { yield return AccountTypes.AzureServicePrincipalAccountType; }
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureWebAppEndpointResource, AzureWebAppEndpoint>();
        }

        public IActionHandler? HealthCheckActionHandlerForTargetType()
        {
            return new AzureWebAppHealthCheckActionHandler();
        }

        public IEnumerable<(string key, object value)> GetFeatureUsage(IEndpointMetricContext context)
        {
            var total = context.GetEndpoints<AzureWebAppEndpoint>().Count();

            yield return ("azurewebapps", total);
        }
    }
}