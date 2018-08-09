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
using Octostache;

namespace Calamari.Integration.Scripting
{

    
    public class CombinedScriptEngine : IScriptRunner
    {
        private readonly IScriptEngineRegistry scriptEngineRegistry;
        private readonly IEnumerable<IScriptWrapper> scriptWrapperHooks;
        private readonly VariableDictionary variables;
        private readonly ICommandLineRunner commandLineRunner;

        /// <summary>
        /// The original default constructor.
        /// </summary>
//        public CombinedScriptEngine()
//        {
//            this.scriptWrapperHooks = Enumerable.Empty<IScriptWrapper>();
//        }
        
//        public CombinedScriptEngine(IEnumerable<IScriptWrapper> scriptWrapperHooks, 
//            VariableDictionary variables,
//            ICommandLineRunner commandLineRunner,
//            StringDictionary environmentVars = null
//            ) : this(ScriptEngineRegistry.Instance, scriptWrapperHooks)
//        {
//        }

        
        public CombinedScriptEngine(IScriptEngineRegistry scriptEngineRegistry,
            IEnumerable<IScriptWrapper> scriptWrapperHooks,
            VariableDictionary variables,
            ICommandLineRunner commandLineRunner
        )
        {
            this.scriptEngineRegistry = scriptEngineRegistry;
            this.scriptWrapperHooks = scriptWrapperHooks;
            this.variables = variables;
            this.commandLineRunner = commandLineRunner;
        }

        public ScriptSyntax[] GetSupportedTypes()
        {
            return (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                ? new[] { ScriptSyntax.Bash, ScriptSyntax.CSharp, ScriptSyntax.FSharp }
                : new[] { ScriptSyntax.PowerShell, ScriptSyntax.CSharp, ScriptSyntax.FSharp };
        }

        public ICommandResult Execute(Shared.Scripting.Script script, StringDictionary environmentVars = null)
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

        private ScriptSyntax ValidateScriptType(Shared.Scripting.Script script)
        {
            var type = ScriptTypeExtensions.FileNameToScriptType(script.File);
            if (!GetSupportedTypes().Contains(type))
                throw new CommandException($"{type} scripts are not supported on this platform");

            return type;
        }
    }
}