using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Data.Model;
using Sashimi.Azure.Common.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Azure.Accounts
{
    class AzureServicePrincipalAccountDetails : AccountDetails, IExpandVariableForAccountDetails
    {
        public override AccountType AccountType { get; } = AccountTypes.AzureServicePrincipalAccountType;

        public string? SubscriptionNumber { get; set; }

        public string? ClientId { get; set; }

        public string? TenantId { get; set; }

        public SensitiveString? Password { get; set; }

        public string? AzureEnvironment { get; set; }
        public string? ResourceManagementEndpointBaseUri { get; set; }
        public string? ActiveDirectoryEndpointBaseUri { get; set; }

        [JsonIgnore]
        public VariableType ExpandsVariableType { get; } = AzureVariableType.AzureServicePrincipal;

        [JsonIgnore]
        public string Authority => $"{ActiveDirectoryEndpointBaseUriOrDefault}{TenantId}";

        string ActiveDirectoryEndpointBaseUriOrDefault => !string.IsNullOrWhiteSpace(ActiveDirectoryEndpointBaseUri)
            ? ActiveDirectoryEndpointBaseUri
            : "https://login.windows.net/";

        public bool CanExpand(string id, string referencedEntityId)
        {
            return id == referencedEntityId;
        }

        public override IEnumerable<(string key, string template)> ContributeResourceLinks()
        {
            yield return ("ResourceGroups", "accounts/{id}/resourceGroups");
            yield return ("StorageAccounts", "accounts/{id}/storageAccounts");
            yield return ("WebSites", "accounts/{id}/websites");
            yield return ("WebSiteSlots", "accounts/{id}/{resourceGroupName}/websites/{webSiteName}/slots");
        }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.Azure.SubscriptionId, SubscriptionNumber);
            yield return new Variable(SpecialVariables.Action.Azure.ClientId, ClientId);
            yield return new Variable(SpecialVariables.Action.Azure.TenantId, TenantId);
            yield return new Variable(SpecialVariables.Action.Azure.Password, Password);

            if (!String.IsNullOrWhiteSpace(AzureEnvironment))
            {
                yield return new Variable(SpecialVariables.Action.Azure.Environment, AzureEnvironment);
            }

            if (!String.IsNullOrWhiteSpace(ResourceManagementEndpointBaseUri))
            {
                yield return new Variable(SpecialVariables.Action.Azure.ResourceManagementEndPoint, ResourceManagementEndpointBaseUri);
            }

            if (!String.IsNullOrWhiteSpace(ActiveDirectoryEndpointBaseUri))
            {
                yield return new Variable(SpecialVariables.Action.Azure.ActiveDirectoryEndPoint, ActiveDirectoryEndpointBaseUri);
            }
        }

        public override IEnumerable<Variable> ExpandVariable(Variable variable)
        {
            if (variable.Type != ExpandsVariableType)
            {
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");
            }

            yield return new Variable($"{variable.Name}.Client", ClientId);
            yield return new Variable($"{variable.Name}.SubscriptionNumber", SubscriptionNumber);
            yield return new Variable($"{variable.Name}.Password", Password);
            yield return new Variable($"{variable.Name}.TenantId", TenantId);
            yield return new Variable($"{variable.Name}.AzureEnvironment", AzureEnvironment);
            yield return new Variable($"{variable.Name}.ActiveDirectoryEndpointBaseUri", ActiveDirectoryEndpointBaseUri);
            yield return new Variable($"{variable.Name}.ResourceManagementEndpointBaseUri", ResourceManagementEndpointBaseUri);
        }

        public IEnumerable<(string property, bool isSensitive)> GetVariableReferencePropertiesToExpand(VariableType variableType)
        {
            if (variableType != ExpandsVariableType)
            {
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");
            }

            yield return ("Client", false);
            yield return ("SubscriptionNumber", false);
            yield return ("Password", true);
            yield return ("TenantId", false);
            yield return ("AzureEnvironment", false);
            yield return ("ActiveDirectoryEndpointBaseUri", false);
            yield return ("ResourceManagementEndpointBaseUri", false);
        }
    }
}