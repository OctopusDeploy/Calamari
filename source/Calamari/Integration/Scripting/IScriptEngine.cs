using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine  
    {
        string[] GetSupportedExtensions();
        CommandResult Execute(Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner);
    }
}