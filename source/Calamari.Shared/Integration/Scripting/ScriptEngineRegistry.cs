using System.Collections.Generic;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Integration.Scripting
{
    public class ScriptEngineRegistry
    {
        private readonly Dictionary<ScriptType, IScriptEngine> scriptEngines = new Dictionary<ScriptType, IScriptEngine>
        {
            {ScriptType.Powershell, new PowerShellScriptEngine() },
            {ScriptType.ScriptCS, new ScriptCSScriptEngine() },
            {ScriptType.Bash, new BashScriptEngine()},
            {ScriptType.FSharp, new FSharpEngine()}
        }; 

        public static readonly ScriptEngineRegistry Instance = new ScriptEngineRegistry();

        public IDictionary<ScriptType, IScriptEngine> ScriptEngines { get { return scriptEngines; } }
    }

}