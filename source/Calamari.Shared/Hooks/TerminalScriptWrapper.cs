using System;
using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Hooks
{
    /// <summary>
    /// The last wrapper in any chain. It calls the script engine directly.
    /// </summary>
    public class TerminalScriptWrapper : ScriptWrapperBase
    {
        readonly IScriptExecutor scriptExecutor;
        readonly IVariables variables;

        public override bool IsEnabled(ScriptSyntax syntax) => true;

        public override int Priority => ScriptWrapperPriorities.TerminalScriptPriority;

        public override IScriptWrapper NextWrapper
        {
            get => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
            set => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
        }

        public TerminalScriptWrapper(IScriptExecutor scriptExecutor, IVariables variables)
        {
            this.scriptExecutor = scriptExecutor;
            this.variables = variables;
        }

        protected override CommandResult ExecuteScriptBase(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars) => 
            scriptExecutor.Execute(script, variables, commandLineRunner, environmentVars);
    }
}
