using System.Collections.Specialized;

namespace Calamari.Shared.Scripting
{
    public interface IScriptRunner
    {
        ScriptSyntax[] GetSupportedTypes();

        ICommandResult Execute(Script script, StringDictionary environmentVars = null);
    }
}