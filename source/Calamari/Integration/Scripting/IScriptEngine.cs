using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine  
    {
        string[] GetSupportedExtensions();
        CommandResult Execute(string scriptFile, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner);
    }
}