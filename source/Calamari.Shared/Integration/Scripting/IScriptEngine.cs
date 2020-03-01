using System.Collections.Generic;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public interface IScriptEngine  
    {
        ScriptSyntax[] GetSupportedTypes();
        CommandResult Execute(
            Script script, 
            IVariables variables, 
            ICommandLineRunner commandLineRunner, 
            Dictionary<string, string> environmentVars = null);
    }
}