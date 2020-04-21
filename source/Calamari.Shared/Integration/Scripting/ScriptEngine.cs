using System;
using Calamari.Commands.Support;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using System.Collections.Generic;
using System.Linq;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.Python;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine
    {
        ScriptSyntax[] GetSupportedTypes();

        CommandResult Execute(
            Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars = null);
    }

    public class ScriptEngine : IScriptEngine
    {
        private readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;

        public ScriptEngine(IEnumerable<IScriptWrapper> scriptWrapperHooks)
        {
            this.scriptWrapperHooks = scriptWrapperHooks;
        }

        public ScriptSyntax[] GetSupportedTypes()
        {
            return ScriptSyntaxHelper.GetPreferenceOrderedScriptSyntaxesForEnvironment();
        }

        public CommandResult Execute(
            Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars = null)
        {
            var syntax = script.File.ToScriptType();
            return BuildWrapperChain(syntax, variables)
                .ExecuteScript(script, syntax, commandLineRunner, variables, environmentVars);
            
            // scriptExecutor.Execute(script, variables, commandLineRunner, environmentVars);
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
                new TerminalScriptWrapper(GetScriptExecutor(scriptSyntax), variables),
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

        IScriptExecutor GetScriptExecutor(ScriptSyntax scriptSyntax)
        {
            switch (scriptSyntax)
            {
                case ScriptSyntax.PowerShell:
                    return new PowerShellScriptExecutor();
                case ScriptSyntax.CSharp:
                    return new ScriptCSScriptExecutor();
                case ScriptSyntax.Bash:
                    return new BashScriptExecutor();
                case ScriptSyntax.FSharp:
                    return new FSharpExecutor();
                case ScriptSyntax.Python:
                    return new PythonScriptExecutor();
                default:
                    throw new NotSupportedException($"{scriptSyntax} script are not supported for execution");
            }
        }
    }
}