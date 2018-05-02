using Calamari.Hooks;
using System.Collections.Specialized;

namespace Calamari.Tests.Hooks
{
    class EnvironmentVariableHook : IScriptEnvironment
    {
        public bool WasCalled { get; private set; } = true;

        public StringDictionary EnvironmentVars
        {
            get
            {
                WasCalled = true;
                return new StringDictionary();
            }
        }
    }
}
