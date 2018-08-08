using System;
using Octostache;

namespace Calamari.Shared.Scripting
{
    /// <summary>
    /// This hook is used to wrap the execution of a script with another script.
    /// </summary>
    public interface IScriptWrapper
    {

//        bool Enabled { get; }

//        /// <summary>
//        /// The next wrapper to call. IScriptWrapper objects essentially form
//        /// a linked list through the NextWrapper property, and scipts are
//        /// wrapped up in multiple wrapper as they move through the list.
//        /// </summary>
//        IScriptWrapper NextWrapper { get; set; }

        //        /// <summary>
//        /// true if this wrapper is enabled, and false otherwise. If
//        /// Enabled is false, this wrapper is not used during execution.
//        /// </summary>
        bool Enabled(VariableDictionary variables);
        
        /// <summary>
        /// Execute the wrapper. The call to this is usually expected to
        /// call the NextWrapper.ExecuteScript() method as it's final step.
        /// </summary>
        void ExecuteScript(IScriptExecutionContext context, Script script, Action<Script> next);
    }
}
