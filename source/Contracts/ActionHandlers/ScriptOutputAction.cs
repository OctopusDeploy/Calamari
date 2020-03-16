using System;
using System.Collections.Generic;

namespace Octopus.Sashimi.Contracts.ActionHandlers
{
    public class ScriptOutputAction
    {
        public string Name { get; set; }

        public IDictionary<string, string> Properties { get; set; }
    }
}