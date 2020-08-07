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
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.AzureWebApp.Endpoints
{
    class AzureWebAppDeploymentTargetTypeProvider : IDeploymentTargetTypeProvider
    {
        public AzureWebAppDeploymentTargetTypeProvider(AzureWebAppServiceMessageHandler azureWebAppServiceMessageHandler)
        {
            CreateTargetServiceMessageHandler = azureWebAppServiceMessageHandler;
        }

        public DeploymentTargetType DeploymentTargetType => AzureWebAppEndpoint.AzureWebAppDeploymentTargetType;
        public Type DomainType => typeof(AzureWebAppEndpoint);
        public Type ApiType => typeof(AzureWebAppEndpointResource);
        public IValidator Validator { get; } = new AzureWebAppEndpointValidator();


        public IEnumerable<AccountType> SupportedAccountTypes
        {
            get { yield return AccountTypes.AzureServicePrincipalAccountType; }
        }

        public void BuildMappings(IResourceMappingsBuilder builder)
        {
            builder.Map<AzureWebAppEndpointResource, AzureWebAppEndpoint>();
        }

        public IActionHandler? HealthCheckActionHandlerForTargetType { get; } = new AzureWebAppHealthCheckActionHandler();

        public IEnumerable<(string key, object value)> GetFeatureUsage(IEndpointMetricContext context)
        {
            var total = context.GetEndpoints<AzureWebAppEndpoint>().Count();

            yield return ("azurewebapps", total);
        }

        public IEnumerable<ScriptFunctionRegistration> GetScriptFunctionRegistrations()
        {
            yield return new ScriptFunctionRegistration("OctopusAzureWebAppTarget",
                                                             "Creates a new Azure WebApp target.",
                                                             CreateTargetServiceMessageHandler!.ServiceMessageName,
                                                             new Dictionary<string, FunctionParameter>
                                                             {
                                                                 { "name", new FunctionParameter(ParameterType.String) },
                                                                 { "webAppName", new FunctionParameter(ParameterType.String) },
                                                                 { "webAppSlot", new FunctionParameter(ParameterType.String) },
                                                                 { "resourceGroupName", new FunctionParameter(ParameterType.String) },
                                                                 { "account", new FunctionParameter(ParameterType.String) },
                                                                 { "roles", new FunctionParameter(ParameterType.String) },
                                                                 { "updateIfExisting", new FunctionParameter(ParameterType.Bool) }
                                                             });
        }

        public ICreateTargetServiceMessageHandler? CreateTargetServiceMessageHandler { get; }
    }
}