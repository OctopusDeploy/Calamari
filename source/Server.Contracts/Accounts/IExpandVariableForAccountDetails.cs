using System;
using System.Collections.Generic;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Accounts
{
    public interface IExpandVariableForAccountDetails
    {
        VariableType ExpandsVariableType { get; }
        bool CanExpand(string id, string referencedEntityId);
        IEnumerable<(string property, bool isSensitive)> GetVariableReferencePropertiesToExpand(VariableType variableType);
        IEnumerable<Variable> ExpandVariable(Variable variable);
    }
}