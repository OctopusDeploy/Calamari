using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Octostache;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServiceServiceMessageHandler : ICreateTargetServiceMessageHandler
    {
        public string AuditEntryDescription => "Azure Cloud Service Target";
        public string ServiceMessageName => AzureCloudServiceServiceMessageNames.CreateTargetName;
        public IEnumerable<ScriptFunctionRegistration> ScriptFunctionRegistrations { get; } = new List<ScriptFunctionRegistration>
        {
            new ScriptFunctionRegistration("OctopusAzureCloudServiceTarget",
                                           "Creates a new Azure Cloud Service target.",
                                           AzureCloudServiceServiceMessageNames.CreateTargetName,
                                           new Dictionary<string, FunctionParameter>
                                           {
                                               { AzureCloudServiceServiceMessageNames.NameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.AzureDeploymentSlotAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.SwapAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.InstanceCountAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.AccountIdOrNameAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.RolesAttribute, new FunctionParameter(ParameterType.String) },
                                               { AzureCloudServiceServiceMessageNames.UpdateIfExistingAttribute, new FunctionParameter(ParameterType.Bool) },
                                               { AzureCloudServiceServiceMessageNames.WorkerPoolIdOrNameAttribute, new FunctionParameter(ParameterType.String) }
                                           })
        };

        public Endpoint BuildEndpoint(IDictionary<string, string> messageProperties,
                                      VariableDictionary variables,
                                      Func<string, string> accountIdResolver,
                                      Func<string, string> certificateIdResolver,
                                      Func<string, string> workerPoolIdResolver,
                                      Func<string,AccountType> accountTypeResolver,
                                      Func<string,string> feedIdResolver,
                                      ITaskLog taskLog)
        {
            // TODO should this be getting the account id as an Azure specific scoped variable

            var endpoint = new AzureCloudServiceEndpoint
            {
                AccountId = GetAccountId(messageProperties, variables, accountIdResolver, taskLog)
            };

            messageProperties.TryGetValue(AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute,
                                          out var cloudServiceName);
            messageProperties.TryGetValue(AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute,
                out var storageAccountName);

            endpoint.CloudServiceName = cloudServiceName;
            endpoint.StorageAccountName = storageAccountName;
            endpoint.Slot = GetSlot(messageProperties);
            endpoint.SwapIfPossible = GetSwap(messageProperties);
            endpoint.UseCurrentInstanceCount = GetUseCurrentInstance(messageProperties);
            endpoint.DefaultWorkerPoolId = GetWorkerPoolId(messageProperties, variables, workerPoolIdResolver);

            return endpoint;
        }

        string? GetWorkerPoolId(IDictionary<string, string> messageProperties, VariableDictionary variables, Func<string, string> workerPoolIdResolver)
        {
            messageProperties.TryGetValue(AzureCloudServiceServiceMessageNames.WorkerPoolIdOrNameAttribute, out var workerPoolIdOrName);

            if (string.IsNullOrWhiteSpace(workerPoolIdOrName))
                // try getting the worker pool from the step variables
                workerPoolIdOrName = variables.Get(KnownVariables.WorkerPool.Id)!;

            if (string.IsNullOrWhiteSpace(workerPoolIdOrName) )
                return null;

            var resolvedWorkerPoolId = workerPoolIdResolver(workerPoolIdOrName);
            if (string.IsNullOrWhiteSpace(resolvedWorkerPoolId))
                return null;

            return resolvedWorkerPoolId;
        }

        string GetAccountId(IDictionary<string, string> messageProperties,
            VariableDictionary variables, Func<string, string> accountIdResolver, ITaskLog taskLog)
        {
            messageProperties.TryGetValue(AzureCloudServiceServiceMessageNames.AccountIdOrNameAttribute, out var accountIdOrName);
            if (!string.IsNullOrWhiteSpace(accountIdOrName))
            {
                var resolvedAccountId = accountIdResolver(accountIdOrName);

                if (!string.IsNullOrWhiteSpace(resolvedAccountId))
                {
                    return resolvedAccountId;
                }
            }

            accountIdOrName = variables.Get(SpecialVariables.Action.Azure.AccountId)!;
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

        static bool GetSwap(IDictionary<string, string> messageProperties)
        {
            var propertyValue = messageProperties[AzureCloudServiceServiceMessageNames.SwapAttribute];
            return string.IsNullOrWhiteSpace(propertyValue) ||
                   !propertyValue.Equals("deploy", StringComparison.OrdinalIgnoreCase);
        }

        static string GetSlot(IDictionary<string, string> messageProperties)
        {
            var propertyValue = messageProperties[AzureCloudServiceServiceMessageNames.AzureDeploymentSlotAttribute];
            if (!string.IsNullOrEmpty(propertyValue) &&
                propertyValue.Equals("production", StringComparison.OrdinalIgnoreCase))
            {
                return AzureCloudServiceEndpointDeploymentSlot.Production;
            }

            return AzureCloudServiceEndpointDeploymentSlot.Staging;
        }

        static bool GetUseCurrentInstance(IDictionary<string, string> messageProperties)
        {
            var propertyValue = messageProperties[AzureCloudServiceServiceMessageNames.InstanceCountAttribute];
            return string.IsNullOrEmpty(propertyValue) ||
                   !propertyValue.Equals("configuration", StringComparison.OrdinalIgnoreCase);
        }

        internal static class AzureCloudServiceServiceMessageNames
        {
            public const string CreateTargetName = "create-azurecloudservicetarget";
            public const string NameAttribute = "name";
            public const string AccountIdOrNameAttribute = "octopusAccountIdOrName";
            public const string AzureCloudServiceNameAttribute = "azureCloudServiceName";
            public const string AzureStorageAccountAttribute = "azureStorageAccount";
            public const string AzureDeploymentSlotAttribute = "azureDeploymentSlot";
            public const string SwapAttribute = "swap";
            public const string InstanceCountAttribute = "instanceCount";
            public const string RolesAttribute = "octopusRoles";
            public const string UpdateIfExistingAttribute = "updateIfExisting";
            public const string WorkerPoolIdOrNameAttribute = "octopusDefaultWorkerPoolIdOrName";
        }

        internal static class AzureCloudServiceEndpointDeploymentSlot
        {
            public const string Staging = "Staging";
            public const string Production = "Production";
        }
    }
}