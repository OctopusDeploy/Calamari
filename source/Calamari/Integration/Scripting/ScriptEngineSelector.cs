using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Integration.Scripting
{

    public class WindowsScriptEngineSelector : ScriptEngineSelector
    {
        static readonly string[] SupportedExtensions = new[] { "csx", "ps1" };
        public override string[] GetSupportedExtensions()
        {
            return SupportedExtensions;
        }
    }

    public class NixScriptEngineSelector : ScriptEngineSelector
    {
        static readonly string[] SupportedExtensions = new[] { "csx", "sh" };
        public override string[] GetSupportedExtensions()
        {
            return SupportedExtensions;
        }
    }

    public abstract class ScriptEngineSelector : IScriptEngineSelector
    {
        public static ScriptEngineSelector GetScriptEngineSelector()
        {
            if (CalamariEnvironment.IsRunningOnNix)
            {
                return new NixScriptEngineSelector();
            }

            return new WindowsScriptEngineSelector();
        }

        public abstract string[] GetSupportedExtensions();

        public IScriptEngine SelectEngine(string scriptFile)
        {
            var extension = Path.GetExtension(scriptFile);
            if (extension != null)
            {
                extension = extension.Trim('.');
                switch (extension.ToLowerInvariant())
                {
                    case "ps1":
                        return new PowerShellScriptEngine();
                    case "csx":
                        return new ScriptCSScriptEngine();
                    case "sh":
                        return new BashScriptEngine();
                }
            }
            
            throw new CommandException("Unsupported script file extension: " + Path.GetFileName(scriptFile));
        }
    }
}