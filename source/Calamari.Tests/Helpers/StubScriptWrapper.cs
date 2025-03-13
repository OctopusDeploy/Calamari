using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;

namespace Calamari.Tests.Helpers
{
    public class StubScriptWrapper : IScriptWrapper
    {
        bool isEnabled;

        public int Priority { get; } = 1;
        public IScriptWrapper NextWrapper { get; set; }

        public bool IsEnabled(ScriptSyntax syntax)
        {
            return isEnabled;
        }

        // We manually enable this wrapper when needed,
        // to avoid this wrapper being auto-registered and called from real programs
        public StubScriptWrapper Enable()
        {
            isEnabled = true;
            return this;
        }

        public CommandResult ExecuteScript(
            Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            return new CommandResult("stub", 0);
        }
    }
}