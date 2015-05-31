using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine  
    {
        string[] GetSupportedExtensions();
        CommandResult Execute(string scriptFile, VariableDictionary variables, ICommandLineRunner commandLineRunner);
    }
}