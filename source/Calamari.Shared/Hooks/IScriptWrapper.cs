using System.Collections.Specialized;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Hooks
{
    /// <summary>
    /// This hook is used to wrap the execution of a script with another script.
    /// </summary>
    public interface IScriptWrapper
    {
        bool Enabled { get; }

        IScriptWrapper NextWrapper { get; set; }

        CommandResult ExecuteScript(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars);

    }
}
