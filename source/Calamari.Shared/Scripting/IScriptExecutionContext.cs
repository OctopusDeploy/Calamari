using System.Collections.Specialized;
using Octostache;

namespace Calamari.Shared.Scripting
{
    public interface IScriptExecutionContext
    {
        ScriptSyntax ScriptSyntax { get; }
        VariableDictionary Variables { get; }
        StringDictionary EnvironmentVariables { get; }
    }
}