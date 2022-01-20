using System;
using System.Collections.Generic;
using FluentValidation;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IDeploymentTargetTypeProvider : IContributeMappings
    {
        DeploymentTargetType DeploymentTargetType { get; }
        Type DomainType { get; }
        Type ApiType { get; }
        IActionHandler? HealthCheckActionHandlerForTargetType { get; }
        IActionHandler? DiscoveryActionHandlerForTargetType => null;
        bool SupportsDiscovery => DiscoveryActionHandlerForTargetType != null;
        IValidator Validator { get; }
        IEnumerable<AccountType> SupportedAccountTypes { get; }
        ICreateTargetServiceMessageHandler? CreateTargetServiceMessageHandler { get; }
        IEnumerable<(string key, object value)> GetFeatureUsage(IEndpointMetricContext context);
    }
}