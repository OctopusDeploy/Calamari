using Calamari.Extensibility;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine  
    {
        string[] GetSupportedExtensions();
        CommandResult Execute(Script script, IVariableDictionary variables, ICommandLineRunner commandLineRunner);
    }
}