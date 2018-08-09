using System.Collections.Generic;
using Calamari.Integration.Scripting.Bash;
using Calamari.Integration.Scripting.FSharp;
using Calamari.Integration.Scripting.ScriptCS;
using Calamari.Integration.Scripting.WindowsPowerShell;
using Calamari.Shared;
using Calamari.Shared.Scripting;

namespace Calamari.Integration.Scripting
{

    public interface IScriptEngineRegistry
    {
        IDictionary<ScriptSyntax, IScriptEngine> ScriptEngines { get; }
    }

    //TODO: Replace with reflection?
    public class ScriptEngineRegistry: IScriptEngineRegistry
    {
        private readonly Dictionary<ScriptSyntax, IScriptEngine> scriptEngines = new Dictionary<ScriptSyntax, IScriptEngine>
        {
            {ScriptSyntax.PowerShell, new PowerShellScriptEngine() },
            {ScriptSyntax.CSharp, new ScriptCSScriptEngine() },
            {ScriptSyntax.Bash, new BashScriptEngine()},
            {ScriptSyntax.FSharp, new FSharpEngine()}
        }; 


        public IDictionary<ScriptSyntax, IScriptEngine> ScriptEngines => scriptEngines;
    }

}