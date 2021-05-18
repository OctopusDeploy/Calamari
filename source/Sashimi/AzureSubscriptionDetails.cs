using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Data.Model;
using Sashimi.Azure.Common.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.AzureCloudService
{
    public class AzureSubscriptionDetails : AccountDetails, IExpandVariableForAccountDetails
    {
        public string? SubscriptionNumber { get; set; }
        public string? CertificateThumbprint { get; set; }
        public string? AzureEnvironment { get; set; }
        public string? ServiceManagementEndpointBaseUri { get; set; }
        public string? ServiceManagementEndpointSuffix { get; set; }
        public SensitiveString? CertificateBytes { get; set; }

        public override AccountType AccountType => AccountTypes.AzureSubscriptionAccountType;

        public override IEnumerable<(string key, string template)> ContributeResourceLinks()
        {
            yield return ("PublicKey", "accounts/{id}/pk");
            yield return ("StorageAccounts", "accounts/{id}/storageAccounts");
            yield return ("WebSites", "accounts/{id}/websites");
            yield return ("WebSiteSlots", "accounts/{id}/{resourceGroupName}/websites/{webSiteName}/slots");
        }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.Azure.SubscriptionId, SubscriptionNumber);
            yield return new Variable(SpecialVariables.Action.Azure.CertificateBytes, CertificateBytes);
            yield return new Variable(SpecialVariables.Action.Azure.CertificateThumbprint, CertificateThumbprint);

            if (!String.IsNullOrWhiteSpace(AzureEnvironment))
            {
                yield return new Variable(SpecialVariables.Action.Azure.Environment, AzureEnvironment);
            }

            if (!String.IsNullOrWhiteSpace(ServiceManagementEndpointBaseUri))
            {
                yield return new Variable(SpecialVariables.Action.Azure.ServiceManagementEndPoint, ServiceManagementEndpointBaseUri);
            }

            if (!String.IsNullOrWhiteSpace(ServiceManagementEndpointSuffix))
            {
                yield return new Variable(SpecialVariables.Action.Azure.StorageEndPointSuffix, ServiceManagementEndpointSuffix);
            }
        }

        public override IEnumerable<Variable> ExpandVariable(Variable variable)
        {
            if (variable.Type != ExpandsVariableType)
            {
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");
            }

            yield return new Variable($"{variable.Name}.CertificateThumbprint", CertificateThumbprint!, VariableType.Sensitive);
            yield return new Variable($"{variable.Name}.SubscriptionNumber", SubscriptionNumber);
            yield return new Variable($"{variable.Name}.AzureEnvironment", AzureEnvironment);
            yield return new Variable($"{variable.Name}.ServiceManagementEndpointBaseUri", ServiceManagementEndpointBaseUri);
            yield return new Variable($"{variable.Name}.ServiceManagementEndpointSuffix", ServiceManagementEndpointSuffix);
        }

        [JsonIgnore]
        public VariableType ExpandsVariableType { get; } = AzureVariableType.AzureServicePrincipal;

        public bool CanExpand(string id, string referencedEntityId)
        {
            return id == referencedEntityId;
        }

        public IEnumerable<(string property, bool isSensitive)> GetVariableReferencePropertiesToExpand(VariableType variableType)
        {
            if (variableType != ExpandsVariableType)
            {
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");
            }

            return new[]
            {
                ("CertificateThumbprint", true),
                ("SubscriptionNumber", false),
                ("AzureEnvironment", false),
                ("ServiceManagementEndpointBaseUri", false),
                ("ServiceManagementEndpointSuffix", false),
            };
        }

        public override Credentials GetCredential()
        {
            return new Credentials(SubscriptionNumber ?? string.Empty, CertificateBytes?.Value ?? string.Empty);
        }
    }
}