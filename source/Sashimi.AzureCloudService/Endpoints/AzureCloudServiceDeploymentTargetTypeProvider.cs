using System;
using System.Linq;
using System.Collections.Generic;
using FluentValidation;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.AzureCloudService.Endpoints
{
    class AzureCloudServiceDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public DeploymentTargetType DeploymentTargetType => AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType;
        public Type DomainType => typeof(AzureCloudServiceEndpoint);
        public Type ApiType => typeof(CloudServiceEndpointResource);
        public IValidator Validator => new AzureCloudServiceEndpointValidator();

        public IEnumerable<AccountType> SupportedAccountTypes
        {
            get { yield return AccountTypes.AzureSubscriptionAccountType; }
        }

        public IActionHandler HealthCheckActionHandlerForTargetType()
        {
            return new AzureCloudServiceHealthCheckActionHandler();
        }

        public IEnumerable<(string key, object value)> GetMetric(IReadOnlyCollection<Endpoint> endpoints)
        {
            var total = endpoints.Count(m => m is AzureCloudServiceEndpoint);

            yield return ("azurecloudservices", total);
        }
    }
}