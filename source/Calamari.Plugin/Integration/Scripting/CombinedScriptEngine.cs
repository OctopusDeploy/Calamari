using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Integration.Processes;
using Calamari.Plugin;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
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

            return ScriptEngineRegistry.Instance.ScriptEngines[scriptType].Execute(
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