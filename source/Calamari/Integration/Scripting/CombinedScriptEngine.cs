using System;
using System.IO;
using System.Linq;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        public string[] GetSupportedExtensions()
        {
            return CalamariEnvironment.IsRunningOnNix
                ? new[] {ScriptType.ScriptCS.FileExtension(), ScriptType.Bash.FileExtension(), ScriptType.FSharp.FileExtension()}
                : new[] {ScriptType.ScriptCS.FileExtension(), ScriptType.Powershell.FileExtension(), ScriptType.FSharp.FileExtension()};
        }

        public CommandResult Execute(Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var scriptType = ValidateScriptType(script);
            return ScriptEngineRegistry.Instance.ScriptEngines[scriptType].Execute(script, variables, commandLineRunner);
        }

        private ScriptType ValidateScriptType(Script script)
        {
            var scriptExtension = Path.GetExtension(script.File).TrimStart('.');
            if (!GetSupportedExtensions().Any(ext => ext.Equals(scriptExtension, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new InvalidOperationException(string.Format("Script type `{0}` unsupported on this platform.", scriptExtension));
            };
            return scriptExtension.ToScriptType();
        }
    }
}