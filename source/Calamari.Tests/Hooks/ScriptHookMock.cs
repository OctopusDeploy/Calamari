using System;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;
using System.Collections.Specialized;
using Calamari.Shared;
using Calamari.Shared.Scripting;
using Octostache;
using Script = Calamari.Shared.Scripting.Script;

namespace Calamari.Tests.Hooks
{
    /// <summary>
    /// A mock script wrapper that we can use to track if it has been executed or not
    /// </summary>
    public class ScriptHookMock : IScriptWrapper
    {
        private readonly bool enabled;

        /// <summary>
        /// This is how we know if this wrapper was called or not
        /// </summary>
        public bool WasCalled { get; private set; } = false;

        public ScriptHookMock(bool enabled = true)
        {
            this.enabled = enabled;
        }
        
        public bool Enabled(VariableDictionary variables)
        {
            return this.enabled;
        }

        public void ExecuteScript(IScriptExecutionContext context, Script script, Action<Script> next)
        {
            WasCalled = true;
            next(script);
        }
    }
}
