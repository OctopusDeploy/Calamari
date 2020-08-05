using System;
using System.Collections.Generic;
using Octopus.Diagnostics;
using Octostache;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppServiceMessageHandler : ICreateTargetServiceMessageHandler
    {
        readonly ILog logger;

        public AzureWebAppServiceMessageHandler(ILog logger)
        {
            this.logger = logger;
        }

        public string AuditEntryDescription => "Azure Web App Target";
        public string ServiceMessageName => AzureWebAppServiceMessageNames.CreateTargetName;

        public Endpoint BuildEndpoint(IDictionary<string, string> messageProperties,
                                      VariableDictionary variables,
                                      Func<string, string> accountIdResolver,
                                      Func<string, string> certificateIdResolver,
                                      Func<string, string> workerPoolIdResolver,
                                      Func<string, AccountType> accountTypeResolver)
        {
            var endpoint = new AzureWebAppEndpoint
            {
                AccountId = GetAccountId(messageProperties, variables, accountIdResolver)
            };

            messageProperties.TryGetValue(AzureWebAppServiceMessageNames.WebAppNameAttribute, out var wepAppName);
            messageProperties.TryGetValue(AzureWebAppServiceMessageNames.ResourceGroupNameAttribute, out var resourceGroupName);
            
            endpoint.WebAppName = wepAppName;
            endpoint.ResourceGroupName = resourceGroupName;

            if (messageProperties.TryGetValue(AzureWebAppServiceMessageNames.WebAppSlotNameAttribute, out var webAppSlotName) &&
                !string.IsNullOrWhiteSpace(webAppSlotName))
            {
                endpoint.WebAppSlotName = webAppSlotName;
            }

            return endpoint;
        }

        string GetAccountId(IDictionary<string, string> messageProperties,
                            VariableDictionary variables, Func<string, string> accountIdResolver)
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
            logger.Error(message);
            throw new Exception(message);
        }

        internal static class AzureWebAppServiceMessageNames
        {
            public const string CreateTargetName = "create-azurewebapptarget";
            public const string AccountIdOrNameAttribute = "account";
            public const string WebAppNameAttribute = "webAppName";
            public const string ResourceGroupNameAttribute = "resourceGroupName";
            public const string WebAppSlotNameAttribute = "webAppSlot";
        }
    }
}