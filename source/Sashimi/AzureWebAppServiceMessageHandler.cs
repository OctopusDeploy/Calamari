using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Octostache;
using Sashimi.AzureAppService.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.AzureAppService
{
    class AzureWebAppServiceMessageHandler : ICreateTargetServiceMessageHandler
    {
        public string AuditEntryDescription => "Azure Web App Target";
        public string ServiceMessageName => AzureWebAppServiceMessageNames.CreateTargetName;

        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = new List<ScriptFunctionRegistration>
        {
            new ScriptFunctionRegistration("OctopusAzureWebAppTarget",
                                           "Creates a new Azure WebApp target.",
                                           AzureWebAppServiceMessageNames.CreateTargetName,
                                           new Dictionary<string, FunctionParameter>
                                           {
                                               { AzureWebAppServiceMessageNames.NameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureWebAppServiceMessageNames.WebAppNameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureWebAppServiceMessageNames.WebAppSlotNameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureWebAppServiceMessageNames.ResourceGroupNameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureWebAppServiceMessageNames.AccountIdOrNameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureWebAppServiceMessageNames.RolesAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureWebAppServiceMessageNames.UpdateIfExistingAttribute, new FunctionParameter(ParameterType.Bool) },
                                               { AzureWebAppServiceMessageNames.WorkerPoolIdOrNameAttribute, new FunctionParameter(ParameterType.String) }
                                           })
        };

        public Endpoint BuildEndpoint(IDictionary<string, string> messageProperties,
                                      VariableDictionary variables,
                                      Func<string, string> accountIdResolver,
                                      Func<string, string> certificateIdResolver,
                                      Func<string, string> workerPoolIdResolver,
                                      Func<string, AccountType> accountTypeResolver,
                                      Func<string, string> feedIdResolver,
                                      ITaskLog taskLog)
        {
            var endpoint = new AzureWebAppEndpoint
            {
                AccountId = GetAccountId(messageProperties, variables, accountIdResolver, taskLog),
            };

            messageProperties.TryGetValue(AzureWebAppServiceMessageNames.WebAppNameAttribute, out var wepAppName);
            messageProperties.TryGetValue(AzureWebAppServiceMessageNames.ResourceGroupNameAttribute, out var resourceGroupName);

            if (wepAppName is null)
                throw new ControlledActionFailedException("App Service name was not provided");
            if (resourceGroupName is null)
                throw new ControlledActionFailedException("Resource group name was not provided");

            endpoint.WebAppName = wepAppName;
            endpoint.ResourceGroupName = resourceGroupName;

            if (messageProperties.TryGetValue(AzureWebAppServiceMessageNames.WebAppSlotNameAttribute, out var webAppSlotName) &&
                !string.IsNullOrWhiteSpace(webAppSlotName))
            {
                endpoint.WebAppSlotName = webAppSlotName;
            }

            endpoint.DefaultWorkerPoolId = GetWorkerPoolId(messageProperties, variables, workerPoolIdResolver);

            return endpoint;
        }

        string? GetWorkerPoolId(IDictionary<string, string> messageProperties, VariableDictionary variables, Func<string, string> workerPoolIdResolver)
        {
            messageProperties.TryGetValue(AzureWebAppServiceMessageNames.WorkerPoolIdOrNameAttribute, out var workerPoolIdOrName);

            if (string.IsNullOrWhiteSpace(workerPoolIdOrName))
                // try getting the worker pool from the step variables
                workerPoolIdOrName = variables.Get(KnownVariables.WorkerPool.Id);

            if (string.IsNullOrWhiteSpace(workerPoolIdOrName) )
                return null;

            var resolvedWorkerPoolId = workerPoolIdResolver.Invoke(workerPoolIdOrName);
            if (string.IsNullOrWhiteSpace(resolvedWorkerPoolId))
                return null;

            return resolvedWorkerPoolId;
        }

        string GetAccountId(IDictionary<string, string> messageProperties,
                            VariableDictionary variables, Func<string, string> accountIdResolver, ITaskLog taskLog)
        {
            messageProperties.TryGetValue(AzureWebAppServiceMessageNames.AccountIdOrNameAttribute, out var accountIdOrName);
            if (!string.IsNullOrWhiteSpace(accountIdOrName))
            {
                var resolvedAccountId = accountIdResolver(accountIdOrName);

                if (!string.IsNullOrWhiteSpace(resolvedAccountId))
                {
                    return resolvedAccountId;
                }
            }

            accountIdOrName = variables.Get(SpecialVariables.Action.Azure.AccountId);
            if (!string.IsNullOrWhiteSpace(accountIdOrName))
            {
                var resolvedAccountId = accountIdResolver(accountIdOrName);

                if (!string.IsNullOrWhiteSpace(resolvedAccountId))
                {
                    return resolvedAccountId;
                }
            }

            var message = $"Account with Id / Name, {accountIdOrName}, not found.";
            taskLog.Error(message);
            throw new Exception(message);
        }

        internal static class AzureWebAppServiceMessageNames
        {
            public const string NameAttribute = "name";
            public const string RolesAttribute = "octopusRoles";
            public const string UpdateIfExistingAttribute = "updateIfExisting";
            public const string CreateTargetName = "create-azurewebapptarget";
            public const string AccountIdOrNameAttribute = "octopusAccountIdOrName";
            public const string WebAppNameAttribute = "azureWebApp";
            public const string ResourceGroupNameAttribute = "azureResourceGroupName";
            public const string WebAppSlotNameAttribute = "azureWebAppSlot";
            public const string WorkerPoolIdOrNameAttribute = "octopusDefaultWorkerPoolIdOrName";
        }
    }
}