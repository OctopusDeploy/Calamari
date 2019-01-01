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
        public int Priority => 1;
        public bool IsEnabled(ScriptSyntax scriptSyntax) => true;
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars)
        {
            WasCalled = true;
            return NextWrapper.ExecuteScript(script, scriptSyntax, variables, commandLineRunner, environmentVars);
        }
    }
}
