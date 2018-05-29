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
        private readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;
    
        /// <summary>
        /// The original default constructor.
        /// </summary>
        public CombinedScriptEngine()
        {
            this.scriptWrapperHooks = Enumerable.Empty<IScriptWrapper>();
        }

        /// <summary>
        /// The Autofac enriched constructor. Autofac will pick this constructor
        /// because it is the constructor with the most parameters that can be
        /// fulfilled by injection.
        /// </summary>
        /// <param name="scriptWrapperHooks">The collecton of IScriptWrapper objects available in autofac</param>
        public CombinedScriptEngine(IEnumerable<IScriptWrapper> scriptWrapperHooks)
        {
            this.scriptWrapperHooks = scriptWrapperHooks;
        }

        public ScriptSyntax[] GetSupportedTypes()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? new[] { ScriptSyntax.CSharp, ScriptSyntax.Bash, ScriptSyntax.FSharp }
                : new[] { ScriptSyntax.CSharp, ScriptSyntax.Powershell, ScriptSyntax.FSharp };
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
                    environmentVars);


        /// <summary>
        /// Script wrappers form a chain, with one wrapper calling the next, much like
        /// a linked list. The last wrapper to be called is the TerminalScriptWrapper,
        /// which simply executes a ScriptEngine without any additional processing.
        /// In this way TerminalScriptWrapper is what actually executes the script
        /// that is to be run, with all other wrappers contributing to the script
        /// context.
        /// </summary>
        /// <param name="scriptSyntax">The type of the script being run</param>
        /// <returns>
        /// The start of the wrapper chain. Because each IScriptWrapper is expected to call its NextWrapper,
        /// calling ExecuteScript() on the start of the chain will result in every part of the chain being
        /// executed, down to the final TerminalScriptWrapper.
        /// </returns>
        IScriptWrapper BuildWrapperChain(ScriptSyntax scriptSyntax) =>
            // get the type of script
            scriptWrapperHooks
                .Where(hook => hook.Enabled)
                .Aggregate(
                // The last wrapper is always the TerminalScriptWrapper
                new TerminalScriptWrapper(ScriptEngineRegistry.Instance.ScriptEngines[scriptSyntax]),
                (IScriptWrapper current, IScriptWrapper next) =>
                {
                    // the next wrapper is pointed to the current one
                    next.NextWrapper = current;
                    // the next wrapper is carried across to the next aggregate call
                    return next;
                });
                 
        
        private ScriptSyntax ValidateScriptType(Script script)
        {
            var scriptExtension = Path.GetExtension(script.File)?.TrimStart('.');
            var type = scriptExtension.ToScriptType();

            if (!GetSupportedTypes().Contains(type))
                throw new CommandException($"{type} scripts are not supported on this platform");

            return type;
        }
    }
}