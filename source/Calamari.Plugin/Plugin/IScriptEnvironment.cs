using System.Collections.Specialized;

namespace Calamari.Plugin
{
    /// <summary>
    /// Plugins that implement this interface can contribute environment variables to the running script.
    /// </summary>
    public interface IScriptEnvironment : ICalamariPlugin
    {
        /// <summary>
        /// A key value collection of environment variables
        /// </summary>
        StringDictionary EnvironmentVars { get; }
    }
}
