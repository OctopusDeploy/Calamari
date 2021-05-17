using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.Accounts
{
    public abstract class AccountDetails
    {
        [JsonIgnore]
        public abstract AccountType AccountType { get; }

        public virtual IEnumerable<Variable> ExpandVariable(Variable variable)
        {
            return Enumerable.Empty<Variable>();
        }

        public virtual IEnumerable<(string key, string template)> ContributeResourceLinks()
        {
            return Enumerable.Empty<(string key, string template)>();
        }

        public abstract IEnumerable<Variable> ContributeVariables();

        public abstract Credentials GetCredential();
    }
}