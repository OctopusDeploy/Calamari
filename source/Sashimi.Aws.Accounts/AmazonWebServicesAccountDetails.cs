using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json;
using Octopus.Data.Model;
using Sashimi.Aws.Common.Variables;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Aws.Accounts
{
    class AmazonWebServicesAccountDetails : AccountDetails, IExpandVariableForAccountDetails
    {
        public override AccountType AccountType { get; } = AccountTypes.AmazonWebServicesAccountType;

        public string? AccessKey { get; set; }

        public SensitiveString? SecretKey { get; set; }

        [JsonIgnore]
        public VariableType ExpandsVariableType => AmazonWebServicesVariableType.AmazonWebServicesAccount;

        public override IEnumerable<Variable> ExpandVariable(Variable variable)
        {
            if (variable.Type != ExpandsVariableType)
                throw new InvalidOperationException($"Can only expand variables for type {ExpandsVariableType}");

            yield return new Variable($"{variable.Name}.AccessKey", AccessKey);
            yield return new Variable($"{variable.Name}.SecretKey", SecretKey);
        }

        public override IEnumerable<Variable> ContributeVariables()
        {
            yield return new Variable(SpecialVariables.Action.Amazon.AccessKey, AccessKey);
            yield return new Variable(SpecialVariables.Action.Amazon.SecretKey, SecretKey);
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
            yield return ("AccessKey", false);
            yield return ("SecretKey", true);
        }

        public override Credentials GetCredential()
        {
            return new Credentials(AccessKey!, SecretKey?.Value!);
        }
    }
}