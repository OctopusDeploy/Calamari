using System;
using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Common.Features.Scripting
{
    /// <summary>
    /// The last wrapper in any chain. It calls the script engine directly.
    /// </summary>
    public class TerminalScriptWrapper : IScriptWrapper
    {
        readonly IScriptExecutor scriptExecutor;
        readonly IVariables variables;

        public bool IsEnabled(ScriptSyntax syntax) => true;

        public int Priority => ScriptWrapperPriorities.TerminalScriptPriority;

        public IScriptWrapper NextWrapper
        {
            get => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
            set => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
        }

        public TerminalScriptWrapper(IScriptExecutor scriptExecutor, IVariables variables)
        {
            this.scriptExecutor = scriptExecutor;
            this.variables = variables;
        }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars) => 
            scriptExecutor.Execute(script, variables, commandLineRunner, environmentVars);
    }
}
