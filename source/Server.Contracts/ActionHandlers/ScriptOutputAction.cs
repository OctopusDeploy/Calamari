using System.Collections.Generic;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ScriptOutputAction
    {
        public ScriptOutputAction(string name, IDictionary<string, string> properties)
        {
            Name = name;
            Properties = properties;
        }

        public string Name { get; }

        public IDictionary<string, string> Properties { get; }
    }
}