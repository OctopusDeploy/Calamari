using Calamari.Commands.Support;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        private readonly IEnumerable<IScriptEnvironment> environmentHooks;
        private readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;
    
        /// <summary>
        /// The original default constructor.
        /// </summary>
        public CombinedScriptEngine()
        {
            this.environmentHooks = Enumerable.Empty<IScriptEnvironment>();
            this.scriptWrapperHooks = Enumerable.Empty<IScriptWrapper>();
        }

        /// <summary>
        /// The Autofac enriched constructor. Autofac will pick this constructor
        /// because it is the constructor with the most parameters that can be
        /// fulfilled by injection.
        /// </summary>
        /// <param name="environmentHooks">The collecton of IScriptEnvironment objects available in autofac</param>
        /// <param name="scriptWrapperHooks">The collecton of IScriptWrapper objects available in autofac</param>
        public CombinedScriptEngine(
            IEnumerable<IScriptEnvironment> environmentHooks, 
            IEnumerable<IScriptWrapper> scriptWrapperHooks)
        {
            this.environmentHooks = environmentHooks;
            this.scriptWrapperHooks = scriptWrapperHooks;
        }

        public ScriptType[] GetSupportedTypes()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? new[] { ScriptType.ScriptCS, ScriptType.Bash, ScriptType.FSharp }
                : new[] { ScriptType.ScriptCS, ScriptType.Powershell, ScriptType.FSharp };
        }

        public CommandResult Execute(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null) =>
                BuildWrapperChain(ValidateScriptType(script)).ExecuteScript(
                    script,
                    variables,
                    commandLineRunner,
                    environmentHooks.MergeDictionaries(environmentVars));


        /// <summary>
        /// Script wrappers form a chain, with one wrapper calling the next, much like
        /// a linked list. The last wrapper to be called is the TerminalScriptWrapper,
        /// which simply executes a ScriptEngine without any additional processing.
        /// In this way TerminalScriptWrapper is what actually executes the script
        /// that is to be run, aith all other wrappers contributing to the script
        /// context.
        /// </summary>
        /// <param name="scriptType">The type of the script being run</param>
        /// <returns>
        /// The start of the wrapper chain. Because each IScriptWrapper is expected to call its NextWrapper,
        /// calling ExecuteScript() on the start of the chain will result in every part of the chain being
        /// executed, down to the final TerminalScriptWrapper.
        /// </returns>
        IScriptWrapper BuildWrapperChain(ScriptType scriptType) =>
            // get the type of script
            scriptWrapperHooks
                .Where(hook => hook.Enabled)
                .Aggregate(
                // The last wrapper is always the TerminalScriptWrapper
                new TerminalScriptWrapper(ScriptEngineRegistry.Instance.ScriptEngines[scriptType]),
                (IScriptWrapper current, IScriptWrapper next) =>
                {
                    // the next wrapper is pointed to the current one
                    next.NextWrapper = current;
                    // the next wrapper is carried across to the next aggregate call
                    return next;
                });
                 
        
        private ScriptType ValidateScriptType(Script script)
        {
            var scriptExtension = Path.GetExtension(script.File)?.TrimStart('.');
            var type = scriptExtension.ToScriptType();

            if (!GetSupportedTypes().Contains(type))
                throw new CommandException($"{type} scripts are not supported on this platform");

            return type;
        }
    }
}