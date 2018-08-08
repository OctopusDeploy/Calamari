using System;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.Collections.Specialized;
using Calamari.Shared;

namespace Calamari.Hooks
{
    
//    /// <summary>
//    /// The last wrapper in any chain. It calls the script engine directly.
//    /// </summary>
//    public class TerminalScriptWrapper : IScriptWrapper
//    {
//        private readonly IScriptEngine scriptEngine;
//
//
//        public TerminalScriptWrapper(IScriptEngine scriptEngine)
//        {
//            this.scriptEngine = scriptEngine;
//        }
//
//        public ICommandResult ExecuteScript(Script script,
//            ScriptSyntax scriptSyntax,
//            CalamariVariableDictionary variables,
//            ICommandLineRunner commandLineRunner,
//            StringDictionary environmentVars) => 
//            scriptEngine.Execute(script, variables, commandLineRunner, environmentVars);
//
//        public void ExecuteScript(IScriptExecutionContext context, IScript script, Action<IScript> next)
//        {
//            
//        }
//    }
}
