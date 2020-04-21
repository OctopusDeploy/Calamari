using System.Collections.Generic;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Tests.Fixtures.Commands
{
    /// <summary>
    /// A mock script wrapper that we can use to track if it has been executed or not
    /// </summary>
    public class ScriptHookMock : ScriptWrapperBase
    {
        /// <summary>
        /// This is how we know if this wrapper was called or not
        /// </summary>
        public static bool WasCalled { get; set; } = false;

        public override int Priority => 1;
        public override bool IsEnabled(ScriptSyntax scriptSyntax) => true;
        public override IScriptWrapper NextWrapper { get; set; }

        protected override CommandResult ExecuteScriptBase(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            WasCalled = true;
            return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, Variables, environmentVars);
        }
    }
}