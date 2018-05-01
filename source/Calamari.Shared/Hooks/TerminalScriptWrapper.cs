using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.Collections.Specialized;

namespace Calamari.Hooks
{
    /// <summary>
    /// The last wrapper in any chain. It calls the script engine directly.
    /// </summary>
    class TerminalScriptWrapper : IScriptWrapper
    {
        private readonly IScriptEngine scriptEngine;

        public bool Enabled { get; } = true;
        public IScriptWrapper NextWrapper { get; set; }

        public TerminalScriptWrapper(IScriptEngine scriptEngine)
        {
            this.scriptEngine = scriptEngine;
        }

        public CommandResult ExecuteScript(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars) => 
            scriptEngine.Execute(script, variables, commandLineRunner, environmentVars);
    }
}
