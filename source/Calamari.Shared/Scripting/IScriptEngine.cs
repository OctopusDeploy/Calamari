using System.Collections.Specialized;
using System.Windows.Input;
using Octostache;

namespace Calamari.Shared.Scripting
{
    public interface IScriptEngine  
    {
        ScriptSyntax[] GetSupportedTypes();

        ICommandResult Execute(Shared.Scripting.Script script);

//        CommandResult Execute(
//            Script script, 
//            VariableDictionary variables, 
//            ICommandLineRunner commandLineRunner, 
//            StringDictionary environmentVars = null);
    }
}