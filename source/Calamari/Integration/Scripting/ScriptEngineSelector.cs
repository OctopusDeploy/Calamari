using System.IO;
using Calamari.Commands.Support;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Integration.Scripting
{
    public static class ScriptEngineSelector
    {
        static readonly string[] SupportedExtensions = new []{ "csx", "ps1" };

        public static string[] GetSupportedExtensions()
        {
            return SupportedExtensions;
        }

        public static IScriptEngine SelectEngine(string scriptFile)
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
                }
            }
            
            throw new CommandException("Unsupported script file extension: " + Path.GetFileName(scriptFile));
        }
    }
}