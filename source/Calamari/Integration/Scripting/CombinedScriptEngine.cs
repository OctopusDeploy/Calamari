using System.IO;
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
            var scriptType = Path.GetExtension(scriptFile).TrimStart('.').ToScriptType();
            return ScriptEngineRegistry.Instance.ScriptEngines[scriptType].Execute(scriptFile, variables, commandLineRunner);
        }
    }
}