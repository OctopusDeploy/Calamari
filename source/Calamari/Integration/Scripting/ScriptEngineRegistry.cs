using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Calamari.Plugins;

namespace Calamari.Integration.Scripting
{
    public class ScriptEngineRegistry
    {
        private readonly Dictionary<ScriptType, IScriptEngine> scriptEngines = new Dictionary<ScriptType, IScriptEngine>
        {
            {ScriptType.Powershell, new PowerShellScriptEngine()},
            {ScriptType.ScriptCS, new ScriptCSScriptEngine()},
            {ScriptType.Bash, new BashScriptEngine()},
            {ScriptType.FSharp, new FSharpEngine()}
        };

        public static readonly ScriptEngineRegistry Instance = new ScriptEngineRegistry();

        public IDictionary<ScriptType, IScriptEngine> ScriptEngines => scriptEngines;

        public void SetPowershellScriptEngine(ICalamariPlugin[] plugins)
        {
            var engines = plugins.Select(p => p.GetPowershellScriptEngine()).ToArray();
            if (engines.Length == 0)
                return;
            if (engines.Length > 1)
                throw new Exception("Multiple plugins want to set the Powersehll script engine");
            ScriptEngineRegistry.Instance.ScriptEngines[ScriptType.Powershell] = engines[0];
        }
    }
}