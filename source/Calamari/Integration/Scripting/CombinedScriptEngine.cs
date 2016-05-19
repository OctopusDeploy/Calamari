using System;
using System.IO;
using System.Linq;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Scripting
{
    public class CombinedScriptEngine : IScriptEngine
    {
        public string[] GetSupportedExtensions()
        {
            return CalamariEnvironment.IsRunningOnNix
                ? new[] {ScriptType.ScriptCS.FileExtension(), ScriptType.Bash.FileExtension()}
                : new[] {ScriptType.ScriptCS.FileExtension(), ScriptType.Powershell.FileExtension()};
        }

        public CommandResult Execute(string scriptFile, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var scriptType = ValidateScriptType(scriptFile);
            return ScriptEngineRegistry.Instance.ScriptEngines[scriptType].Execute(scriptFile, variables, commandLineRunner);
        }

        private ScriptType ValidateScriptType(string scriptFile)
        {
            var scriptExtension = Path.GetExtension(scriptFile).TrimStart('.');
            if (!GetSupportedExtensions().Any(ext => ext.Equals(scriptExtension, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new InvalidOperationException(string.Format("Script type `{0}` unsupported on this platform.", scriptExtension));
            };
            return scriptExtension.ToScriptType();
        }
    }
}