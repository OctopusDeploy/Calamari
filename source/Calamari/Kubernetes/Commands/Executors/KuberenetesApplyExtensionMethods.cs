using System;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Kubernetes.Commands.Executors
{
    static class KuberenetesApplyExtensionMethods
    {
        public static string[] AddOptionsForServerSideApply(this string[] executeArgs, IVariables variables, ILog log)
        {
            if (variables.GetFlag(SpecialVariables.ServerSideApplyEnabled))
            {
                log.Verbose("Server-side apply is enabled. Applying with field manager 'octopus'");
                
                executeArgs = executeArgs.Concat(new[] {"--server-side", "--field-manager", "octopus"}).ToArray();
                if(variables.GetFlag(SpecialVariables.ServerSideApplyForceConflicts))
                {
                    log.Verbose("Force conflicts is enabled. Field manager 'octopus' will be the sole manager.");
                    executeArgs = executeArgs.Concat(new[] {"--force-conflicts"}).ToArray();
                }
            }

            return executeArgs;
        }
    }
}