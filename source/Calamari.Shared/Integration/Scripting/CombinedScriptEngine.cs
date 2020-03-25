using Calamari.Hooks;
using Calamari.Integration.Processes;
using System.Collections.Generic;
using System.Linq;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        private readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;

        public CombinedScriptEngine(IEnumerable<IScriptWrapper> scriptWrapperHooks)
        {
            this.scriptWrapperHooks = scriptWrapperHooks;
        }

        public ScriptSyntax[] GetSupportedTypes()
        {
            var preferredScriptSyntax = new [] { ScriptSyntaxHelper.GetPreferredScriptSyntaxForEnvironment() };
            var scriptSyntaxesSupportedOnAllPlatforms =  new[] { ScriptSyntax.Python, ScriptSyntax.CSharp, ScriptSyntax.FSharp, ScriptSyntax.PowerShell, ScriptSyntax.Bash };

            //order is important, as we want to try the preferred syntax first
            return preferredScriptSyntax.Concat(scriptSyntaxesSupportedOnAllPlatforms.Except(preferredScriptSyntax)).ToArray();
        }

        public CommandResult Execute(
            Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars = null)
        {
            var syntax = script.File.ToScriptType();
            return BuildWrapperChain(syntax, variables)
                .ExecuteScript(script, syntax, commandLineRunner, environmentVars);
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
        /// <param name="variables"></param>
        /// <returns>
        /// The start of the wrapper chain. Because each IScriptWrapper is expected to call its NextWrapper,
        /// calling ExecuteScript() on the start of the chain will result in every part of the chain being
        /// executed, down to the final TerminalScriptWrapper.
        /// </returns>
        IScriptWrapper BuildWrapperChain(ScriptSyntax scriptSyntax, IVariables variables) =>
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
                new TerminalScriptWrapper(ScriptEngineRegistry.Instance.ScriptEngines[scriptSyntax], variables),
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
    }
}