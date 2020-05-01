using System.Collections.Generic;
using Calamari.Integration.Processes;

namespace Calamari.Common.Features.Scripting
{
    public interface IScriptExecutor  
    {
        CommandResult Execute(
            Script script, 
            IVariables variables, 
            ICommandLineRunner commandLineRunner, 
            Dictionary<string, string> environmentVars = null);
    }
}