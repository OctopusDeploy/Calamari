using System.Collections.Specialized;
using Calamari.Shared.Scripting;
using Octostache;

namespace Calamari.Shared
{
    public class ScriptExecutionContext : IScriptExecutionContext
    {
        public ScriptSyntax ScriptSyntax { get; set; }
        public VariableDictionary Variables { get; set;}
        public StringDictionary EnvironmentVariables { get; set;}
    }
}