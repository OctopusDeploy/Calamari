using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;

namespace Calamari.Tests.Fixtures.Commands
{
    /// <summary>
    /// A mock script wrapper that we can use to track if it has been executed or not
    /// </summary>
    public class ScriptHookMock : IScriptWrapper
    {
        /// <summary>
        /// This is how we know if this wrapper was called or not
        /// </summary>
        public static bool WasCalled { get; set; } = false;

        public int Priority => 1;
        public bool IsEnabled(ScriptSyntax scriptSyntax) => true;
        public IScriptWrapper NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars)
        {
            WasCalled = true;
            return NextWrapper.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }
    }
}