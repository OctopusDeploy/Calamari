﻿using System.Collections.Generic;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting;

namespace Calamari.Hooks
{
    public abstract class ScriptWrapperBase : IScriptWrapper
    {
        protected IVariables Variables;
        public abstract IScriptWrapper NextWrapper { get; set; }
        public abstract int Priority { get; }
        public abstract bool IsEnabled(ScriptSyntax syntax);

        public CommandResult ExecuteScript(
            Script script, 
            ScriptSyntax scriptSyntax, 
            ICommandLineRunner commandLineRunner,
            IVariables inputVariables,
            Dictionary<string, string> environmentVars)
        {
            Variables = inputVariables.Clone();

            return ExecuteScriptBase(script, scriptSyntax, commandLineRunner, environmentVars);
        }

        protected abstract CommandResult ExecuteScriptBase(
            Script script, 
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars);
    }
    
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
        /// true if this wrapper is enabled, and false otherwise. If
        /// Enabled is false, this wrapper is not used during execution.
        /// </summary>
        bool IsEnabled(ScriptSyntax syntax);

        /// <summary>
        /// The next wrapper to call. IScriptWrapper objects essentially form
        /// a linked list through the NextWrapper property, and scipts are
        /// wrapped up in multiple wrapper as they move through the list.
        /// </summary>
        IScriptWrapper NextWrapper { get; set; }

        /// <summary>
        /// Execute the wrapper. The call to this is usually expected to
        /// call the NextWrapper.ExecuteScript() method as it's final step.
        /// </summary>
        CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            IVariables variables,
            Dictionary<string, string> environmentVars);
    }
}
