using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace Calamari.Plugin.Plugin
{
    /// <summary>
    /// Plugins that implement this interface can contribute environment variables to the running script.
    /// </summary>
    interface ScriptEnvironment
    {
        /// <summary>
        /// A key value collection of environment variables
        /// </summary>
        StringDictionary EnvironmentVars { get; }
    }
}
