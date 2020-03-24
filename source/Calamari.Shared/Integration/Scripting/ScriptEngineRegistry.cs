using System.Collections.Generic;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.Python;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;

namespace Calamari.Integration.Scripting
{
    public class ScriptEngineRegistry
    {
        private readonly Dictionary<ScriptSyntax, IScriptExecutor> scriptEngines = new Dictionary<ScriptSyntax, IScriptExecutor>
        {
            {ScriptSyntax.PowerShell, new PowerShellScriptExecutor() },
            {ScriptSyntax.CSharp, new ScriptCSScriptExecutor() },
            {ScriptSyntax.Bash, new BashScriptExecutor()},
            {ScriptSyntax.FSharp, new FSharpExecutor()},
            {ScriptSyntax.Python, new PythonScriptExecutor()}
        }; 

        public static readonly ScriptEngineRegistry Instance = new ScriptEngineRegistry();

        public IDictionary<ScriptSyntax, IScriptExecutor> ScriptEngines { get { return scriptEngines; } }
    }

}