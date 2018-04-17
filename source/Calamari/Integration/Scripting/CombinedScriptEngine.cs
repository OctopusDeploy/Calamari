using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        private readonly string[] scriptEngineDecorators;

        public CombinedScriptEngine()
        {
            this.scriptEngineDecorators = null;
        }

        public CombinedScriptEngine(string[] scriptEngineDecorators)
        {
            this.scriptEngineDecorators = scriptEngineDecorators;
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
            StringDictionary environmentVars = null)
        {
            var scriptType = ValidateScriptType(script);

            return ScriptEngineRegistry.Instance.GetScriptEngine(scriptEngineDecorators, scriptType).Execute(
                script, 
                variables, 
                commandLineRunner, 
                environmentVars);
        }

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