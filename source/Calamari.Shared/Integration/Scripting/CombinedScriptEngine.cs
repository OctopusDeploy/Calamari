using Calamari.Commands.Support;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using System.Collections.Generic;
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
        /// <param name="scriptWrapperHooks">The collection of IScriptWrapper objects available in autofac</param>
        public CombinedScriptEngine(IEnumerable<IScriptWrapper> scriptWrapperHooks)
        {
            this.scriptWrapperHooks = scriptWrapperHooks;
        }


        public ScriptSyntax[] GetSupportedTypes()
        {
            var preferredScriptSyntax = new [] { ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment() };
            var scriptSyntaxesSupportedOnAllPlatforms =  new[] { ScriptSyntax.PowerShell, ScriptSyntax.CSharp, ScriptSyntax.FSharp, ScriptSyntax.Python };

            return preferredScriptSyntax.Concat(scriptSyntaxesSupportedOnAllPlatforms).Distinct().ToArray();
        }
        
        public CommandResult Execute(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars = null)
        {
            var syntax = ValidateScriptType(script);
            return BuildWrapperChain(syntax)
                .ExecuteScript(script, syntax, variables, commandLineRunner, environmentVars);
        }
            


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
                .Where(hook => hook.IsEnabled(scriptSyntax))
                /*
                 * Sort the list in descending order of priority to ensure that
                 * authentication script wrappers are called before any tool
                 * script wrapper that might rely on the auth having being performed
                 */
                .OrderByDescending(hook => hook.Priority)
                .Aggregate(
                // The last wrapper is always the TerminalScriptWrapper
                new TerminalScriptWrapper(ScriptEngineRegistry.Instance.ScriptEngines[scriptSyntax]),
                (IScriptWrapper current, IScriptWrapper next) =>
                {
                    // the next wrapper is pointed to the current one
                    next.NextWrapper = current;
                    /*
                     * The next wrapper is carried across to the next aggregate call,
                     * or is returned as the result of the aggregate call. This means
                     * the last item in the list is the return value.
                     */ 
                    return next;
                });
                 
        
        private ScriptSyntax ValidateScriptType(Script script)
        {
            var type = ScriptTypeExtensions.FileNameToScriptType(script.File);
            if (!GetSupportedTypes().Contains(type))
                throw new CommandException($"{type} scripts are not supported on this platform");

            return type;
        }
    }
}