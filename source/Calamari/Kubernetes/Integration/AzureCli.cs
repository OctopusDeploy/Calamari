using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration;

public class AzureCli : CommandLineTool
{
    public AzureCli(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars) 
        : base(log, commandLineRunner, workingDirectory, environmentVars)
    {
    }

    public bool TrySetAz()
    {
        var foundExecutable = CalamariEnvironment.IsRunningOnWindows
            ? ExecuteCommandAndReturnOutput("where", "az.cmd").FirstOrDefault()
            : ExecuteCommandAndReturnOutput("which", "az").FirstOrDefault();

        if (string.IsNullOrEmpty(foundExecutable))
        {
            log.Error("Could not find az. Make sure az is on the PATH.");
            return false;
        }

        ExecutableLocation = foundExecutable.Trim();
        return true;
    }
}