using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.Collections.Specialized;

namespace Calamari.Tests.Hooks
{
    public class ScriptHookMock : IScriptWrapper
    {
        public bool WasCalled { get; private set; } = true;
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
