using System;
using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Hooks
{
    /// <summary>
    /// The last wrapper in any chain. It calls the script engine directly.
    /// </summary>
    public class TerminalScriptWrapper : IScriptWrapper
    {
        readonly IScriptEngine scriptEngine;

        public bool IsEnabled(ScriptSyntax syntax) => true;

        public int Priority => ScriptWrapperPriorities.TerminalScriptPriority;

        public IScriptWrapper NextWrapper
        {
            get => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
            set => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
        }

        public TerminalScriptWrapper(IScriptEngine scriptEngine)
        {
            this.scriptEngine = scriptEngine;
        }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars) => 
            scriptEngine.Execute(script, variables, commandLineRunner, environmentVars);
    }
}
