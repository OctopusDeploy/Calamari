using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting
{
    /// <summary>
    /// The last wrapper in any chain. It calls the script engine directly.
    /// </summary>
    public class TerminalScriptWrapper : IScriptWrapper
    {
        readonly IScriptExecutor scriptExecutor;
        readonly IVariables variables;

        public TerminalScriptWrapper(IScriptExecutor scriptExecutor, IVariables variables)
        {
            this.scriptExecutor = scriptExecutor;
            this.variables = variables;
        }

        public int Priority => ScriptWrapperPriorities.TerminalScriptPriority;

        public IScriptWrapper? NextWrapper
        {
            get => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
            set => throw new MethodAccessException("TerminalScriptWrapper does not have a NextWrapper");
        }

        public bool IsEnabled(ScriptSyntax syntax)
        {
            return true;
        }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars)
        {
            return scriptExecutor.Execute(script, variables, commandLineRunner, environmentVars);
        }
    }
}