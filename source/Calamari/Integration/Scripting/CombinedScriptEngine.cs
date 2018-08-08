using System;
using Calamari.Commands.Support;
using Calamari.Hooks;
using Calamari.Integration.Processes;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Calamari.Shared;
using Calamari.Shared.Scripting;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        private readonly IScriptEngineRegistry scriptEngineRegistry;
        private readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;
    
        /// <summary>
        /// The original default constructor.
        /// </summary>
        public CombinedScriptEngine()
        {
            this.scriptWrapperHooks = Enumerable.Empty<IScriptWrapper>();
        }
        
        public CombinedScriptEngine(IEnumerable<IScriptWrapper> scriptWrapperHooks) : this(ScriptEngineRegistry.Instance, scriptWrapperHooks)
        {
        }

        /// <summary>
        /// The Autofac enriched constructor. Autofac will pick this constructor
        /// because it is the constructor with the most parameters that can be
        /// fulfilled by injection.
        /// </summary>
        /// <param name="scriptEngineRegistry"></param>
        /// <param name="scriptWrapperHooks">The collecton of IScriptWrapper objects available in autofac</param>
        public CombinedScriptEngine(IScriptEngineRegistry scriptEngineRegistry, IEnumerable<IScriptWrapper> scriptWrapperHooks)
        {
            this.scriptEngineRegistry = scriptEngineRegistry;
            this.scriptWrapperHooks = scriptWrapperHooks;
        }

        public ScriptSyntax[] GetSupportedTypes()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? new[] { ScriptSyntax.Bash, ScriptSyntax.CSharp, ScriptSyntax.FSharp }
                : new[] { ScriptSyntax.PowerShell, ScriptSyntax.CSharp, ScriptSyntax.FSharp };
        }

        public CommandResult Execute(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null)
        {
            var ctx = new ScriptExecutionContext()
            {
                EnvironmentVariables = environmentVars,
                ScriptSyntax = ValidateScriptType(script),
                Variables = variables
            };

            CommandResult final = null;
            scriptWrapperHooks
                .Where(k => k.Enabled(variables))
                .Aggregate((Action<IScriptExecutionContext, Shared.Scripting.Script>) ((a, vv) =>
                    {
                        var engine = scriptEngineRegistry.ScriptEngines[ctx.ScriptSyntax];
                        final = engine.Execute(new Script(vv.File, vv.Parameters),
                            (CalamariVariableDictionary) ctx.Variables,
                            commandLineRunner, environmentVars);
                    }),
                    (inner, wrapper) =>
                    {
                        return ((ctx1, script1) => wrapper.ExecuteScript(ctx, script1, (k) => inner(ctx, k)));
                    })(ctx, new Shared.Scripting.Script(script.File, script.Parameters));

            return final;
        }





        private ScriptSyntax ValidateScriptType(Script script)
        {
            var type = ScriptTypeExtensions.FileNameToScriptType(script.File);
            if (!GetSupportedTypes().Contains(type))
                throw new CommandException($"{type} scripts are not supported on this platform");

            return type;
        }
    }
}