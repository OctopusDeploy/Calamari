using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting.Bash;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Features.Scripting.FSharp;
using Calamari.Common.Features.Scripting.Python;
using Calamari.Common.Features.Scripting.WindowsPowerShell;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting
{
    public interface IScriptEngine
    {
        ScriptSyntax[] GetSupportedTypes();

        CommandResult Execute(
            Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars = null);
    }

    public class ScriptEngine : IScriptEngine
    {
        readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;

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
            Dictionary<string, string>? environmentVars = null)
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
        IScriptWrapper BuildWrapperChain(ScriptSyntax scriptSyntax, IVariables variables)
        {
            // get the type of script
            return scriptWrapperHooks
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
        }

        IScriptExecutor GetScriptExecutor(ScriptSyntax scriptSyntax)
        {
            switch (scriptSyntax)
            {
                case ScriptSyntax.PowerShell:
                    return new PowerShellScriptExecutor();
                case ScriptSyntax.CSharp:
                    return new DotnetScriptExecutor();
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