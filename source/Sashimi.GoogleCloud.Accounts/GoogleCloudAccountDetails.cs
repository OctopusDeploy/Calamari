using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Octopus.Data.Model;
using Sashimi.GoogleCloud.Accounts.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.GoogleCloud.Accounts
{
    class GoogleCloudAccountDetails : AccountDetails, IExpandVariableForAccountDetails
    {
        public override AccountType AccountType { get; } = AccountTypes.GoogleCloudAccountType;

        public string? ServiceAccountEmail { get; set; }

        public SensitiveString? Json { get; set; }

        [JsonIgnore]
        public VariableType ExpandsVariableType => GoogleCloudVariableType.GoogleCloudAccount;

        public override IEnumerable<Variable> ExpandVariable(Variable variable)
        {
            if (variable.Type != ExpandsVariableType)
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");

            yield return new Variable($"{variable.Name}.AccessKey", ServiceAccountEmail);
            yield return new Variable($"{variable.Name}.SecretKey", Json);
        }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.GoogleCloud.ServiceAccountEmail, ServiceAccountEmail);
            yield return new Variable(SpecialVariables.Action.GoogleCloud.Json, Json);
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