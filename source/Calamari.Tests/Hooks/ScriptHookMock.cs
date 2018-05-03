using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.Collections.Specialized;

namespace Calamari.Tests.Hooks
{
    /// <summary>
    /// A mock script wrapper that we can use to track if it has been executed or not
    /// </summary>
    public class ScriptHookMock : IScriptWrapper
    {
        /// <summary>
        /// This is how we know if this wrapper was called or not
        /// </summary>
        public bool WasCalled { get; private set; } = false;
        public bool Enabled { get; } = true;
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            WasCalled = true;
            return NextWrapper.ExecuteScript(script, variables, commandLineRunner, environmentVars);
        }
    }
}
