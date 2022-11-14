using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Kubernetes.Integration
{
    public class GCloud : CommandLineTool
    {
        public GCloud(ILog log, ICommandLineRunner commandLineRunner, string workingDirectory, Dictionary<string, string> environmentVars) 
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
        }

        public bool TrySetGcloud()
        {
            var foundExecutable = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "gcloud.cmd").FirstOrDefault()
                : ExecuteCommandAndReturnOutput("which", "gcloud").FirstOrDefault();

            if (string.IsNullOrEmpty(foundExecutable))
                return false;

            ExecutableLocation = foundExecutable?.Trim();
            return true;
        }
    }
}