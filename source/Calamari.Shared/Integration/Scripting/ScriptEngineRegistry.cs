using System.Collections.Generic;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Integration.Scripting
{
    public class ScriptEngineRegistry
    {
        private readonly Dictionary<ScriptSyntax, IScriptEngine> scriptEngines = new Dictionary<ScriptSyntax, IScriptEngine>
        {
            {ScriptSyntax.Powershell, new PowerShellScriptEngine() },
            {ScriptSyntax.CSharp, new ScriptCSScriptEngine() },
            {ScriptSyntax.Bash, new BashScriptEngine()},
            {ScriptSyntax.FSharp, new FSharpEngine()}
        }; 

        public static readonly ScriptEngineRegistry Instance = new ScriptEngineRegistry();

        public IDictionary<ScriptSyntax, IScriptEngine> ScriptEngines { get { return scriptEngines; } }
    }

}