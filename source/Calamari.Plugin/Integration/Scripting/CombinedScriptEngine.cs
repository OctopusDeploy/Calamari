using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Calamari.Hooks;
using Octopus.CoreUtilities.Extensions;

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

        public CombinedScriptEngine(IEnumerable<IScriptEnvironment> environmentHooks, IEnumerable<IScriptWrapper> scriptWrapperHooks)
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
                // First try to run the script wrapped up by a IScriptWrapper
                RunScriptWithWrapper(script, variables, commandLineRunner, environmentVars) ??
                    // Failing that, run the script engine directly
                    RunScriptWithoutWrapper(script, variables, commandLineRunner, environmentVars);

        /// <summary>
        /// Run a plain script without a wrapper
        /// </summary>
        private CommandResult RunScriptWithoutWrapper(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null) =>
            ValidateScriptType(script)
                .Map(scriptType => ScriptEngineRegistry.Instance.ScriptEngines[scriptType].Execute(
                    script,
                    variables,
                    commandLineRunner,
                    environmentHooks.MergeDictionaries(environmentVars)));            

        /// <summary>
        /// This is the hook point for any wrapper scripts
        /// </summary>
        /// <returns>The command result if a wrapper was used, and null if there were no wrappers</returns>
        private CommandResult RunScriptWithWrapper(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null) =>
            scriptWrapperHooks
                // find the first enabled wrapper
                .FirstOrDefault(scriptHook => scriptHook.Enabled)?
                // Run the wrapper
                .ExecuteScript(
                    script,
                    variables,
                    commandLineRunner,
                    environmentHooks.MergeDictionaries(environmentVars));
        
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