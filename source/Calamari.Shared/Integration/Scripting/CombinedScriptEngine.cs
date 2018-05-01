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
    
        public CombinedScriptEngine()
        {
            this.environmentHooks = Enumerable.Empty<IScriptEnvironment>();
            this.scriptWrapperHooks = Enumerable.Empty<IScriptWrapper>();
        }

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
        /// Script wrappers form a chain, with one wrapper calling the next. The last
        /// wrapper to be called is the TerminalScriptWrapper, which simply executes
        /// a ScriptEngine without any additional processing.
        /// </summary>
        /// <param name="scriptType">The type of the script being run</param>
        /// <returns>The start of the wrapper chain. Executing this wrapper will cause the chain to be executed.</returns>
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