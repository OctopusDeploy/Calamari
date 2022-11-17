using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    public class GkeGcloudAuthPlugin : CommandLineTool
    {
        public GkeGcloudAuthPlugin(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
        }

        public bool ExistsOnPath()
        {
            var foundExecutable = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "gke-gcloud-auth-plugin.exe").FirstOrDefault()
                : ExecuteCommandAndReturnOutput("which", "gke-gcloud-auth-plugin").FirstOrDefault();

            return !string.IsNullOrEmpty(foundExecutable);
        }
    }
}
