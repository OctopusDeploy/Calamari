using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripts;

namespace Calamari.Common.Features.Scripting
{
    /// <summary>
    /// This hook is used to wrap the execution of a script with another script.
    /// </summary>
    public interface IScriptWrapper
    {
        /// <summary>
        /// The priority of the wrapper. Higher priority scripts are
        /// run first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// The next wrapper to call. IScriptWrapper objects essentially form
        /// a linked list through the NextWrapper property, and scipts are
        /// wrapped up in multiple wrapper as they move through the list.
        /// </summary>
        IScriptWrapper? NextWrapper { get; set; }

        /// <summary>
        /// true if this wrapper is enabled, and false otherwise. If
        /// Enabled is false, this wrapper is not used during execution.
        /// </summary>
        bool IsEnabled(ScriptSyntax syntax);

        /// <summary>
        /// Execute the wrapper. The call to this is usually expected to
        /// call the NextWrapper.ExecuteScript() method as it's final step.
        /// </summary>
        CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars);
    }
}