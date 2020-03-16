using System.Collections.Generic;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public class ScriptOutputAction
    {
        public string Name { get; set; }

        public IDictionary<string, string> Properties { get; set; }
    }
}