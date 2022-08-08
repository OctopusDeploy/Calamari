using System;
using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting
{
    public interface IScriptExecutor
    {
        CommandResult Execute(
            Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars = null);
    }
}