using System.Collections.Specialized;

namespace Calamari.Hooks
{
    /// <summary>
    /// Hooks that implement this interface can contribute environment variables to the running script.
    /// </summary>
    public interface IScriptEnvironment : ICalamariHook
    {
        /// <summary>
        /// A key value collection of environment variables
        /// </summary>
        StringDictionary EnvironmentVars { get; }
    }
}
