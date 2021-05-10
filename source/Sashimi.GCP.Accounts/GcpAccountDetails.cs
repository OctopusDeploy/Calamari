using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Data.Model;
using Sashimi.GCP.Accounts.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GCP.Accounts
{
    class GcpAccountDetails : AccountDetails, IExpandVariableForAccountDetails
    {
        public override AccountType AccountType { get; } = AccountTypes.GcpAccountType;

        public string? ServiceAccountEmail { get; set; }

        public SensitiveString? Json { get; set; }

        [JsonIgnore]
        public VariableType ExpandsVariableType => GcpVariableType.GcpAccount;

        public override IEnumerable<Variable> ExpandVariable(Variable variable)
        {
            if (variable.Type != ExpandsVariableType)
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");

            yield return new Variable($"{variable.Name}.AccessKey", ServiceAccountEmail);
            yield return new Variable($"{variable.Name}.SecretKey", Json);
        }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.Gcp.ServiceAccountEmail, ServiceAccountEmail);
            yield return new Variable(SpecialVariables.Action.Gcp.Json, Json);
        }

        public bool CanExpand(string id, string referencedEntityId)
        {
            return id == referencedEntityId;
        }

        public IEnumerable<(string property, bool isSensitive)> GetVariableReferencePropertiesToExpand(VariableType variableType)
        {
            if (variableType != ExpandsVariableType)
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");

            yield return ("Name", false);
            yield return ("ServiceAccountEmail", false);
            yield return ("Json", true);
        }
    }
}