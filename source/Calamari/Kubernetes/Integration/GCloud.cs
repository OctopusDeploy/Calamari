using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.FileSystem;
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

        public void ConfigureGcloudAccount(string project, string region, string zone, string jsonKey, bool useVmServiceAccount, string impersonationEmails)
        {
            if (!string.IsNullOrEmpty(project))
            {
                environmentVars.Add("CLOUDSDK_CORE_PROJECT", project);
            }

            if (!string.IsNullOrEmpty(region))
            {
                environmentVars.Add("CLOUDSDK_COMPUTE_REGION", region);
            }

            if (!string.IsNullOrEmpty(zone))
            {
                environmentVars.Add("CLOUDSDK_COMPUTE_ZONE", zone);
            }

            if (!useVmServiceAccount)
            {
                if (jsonKey == null)
                {
                    log.Error("Failed to authenticate with gcloud. Key file is empty.");
                    return;
                }

                log.Verbose("Authenticating to gcloud with key file");
                var bytes = Convert.FromBase64String(jsonKey);
                using (var keyFile = new TemporaryFile(Path.Combine(workingDirectory, "gcpJsonKey.json")))
                {
                    File.WriteAllBytes(keyFile.FilePath, bytes);
                    var result = ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, "auth", "activate-service-account", $"--key-file=\"{keyFile.FilePath}\""));
                    result.VerifySuccess();
                }

                log.Verbose("Successfully authenticated with gcloud");
            }
            else
            {
                log.Verbose("Bypassing authentication with gcloud");
            }

            if (!string.IsNullOrEmpty(impersonationEmails))
                environmentVars.Add("CLOUDSDK_AUTH_IMPERSONATE_SERVICE_ACCOUNT", impersonationEmails);
        }

        public void ConfigureGkeKubeCtlAuthentication(Kubectl kubectlCli, string gkeClusterName, string region, string zone, string @namespace)
        {
            log.Info($"Creating kubectl context to GKE Cluster called {gkeClusterName} (namespace {@namespace}) using a Google Cloud Account");

            var arguments = new List<string>(new[]
            {
                "container",
                "clusters",
                "get-credentials",
                gkeClusterName
            });

            if (!string.IsNullOrWhiteSpace(zone))
            {
                arguments.Add($"--zone={zone}");
            }
            else if (!string.IsNullOrWhiteSpace(region))
            {
                arguments.Add($"--region={region}");
            }
            else
            {
                throw new ArgumentException("Either zone or region must be defined.");
            }

            var result = ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, arguments.ToArray()));
            result.VerifySuccess();
            kubectlCli.ExecuteCommandAndAssertSuccess("config", "set-context", "--current", $"--namespace={@namespace}");
        }
    }
}