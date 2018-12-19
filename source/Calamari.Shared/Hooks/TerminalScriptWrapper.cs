﻿using System;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.Collections.Specialized;

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
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars) => 
            scriptEngine.Execute(script, variables, commandLineRunner, environmentVars);
    }
}
