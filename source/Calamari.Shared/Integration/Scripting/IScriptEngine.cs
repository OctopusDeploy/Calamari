using System.Collections.Generic;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine  
    {
        ScriptSyntax[] GetSupportedTypes();
        CommandResult Execute(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner, 
            Dictionary<string, string> environmentVars = null);
    }
}