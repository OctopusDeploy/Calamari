﻿using System;
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
        private readonly ICalamariFileSystem fileSystem;

        public GCloud(ILog log, ICommandLineRunner commandLineRunner, ICalamariFileSystem fileSystem, string workingDirectory, Dictionary<string, string> environmentVars)
            : base(log, commandLineRunner, workingDirectory, environmentVars)
        {
            this.fileSystem = fileSystem;
        }

        public void SetGcloud()
        {
            var result = CalamariEnvironment.IsRunningOnWindows
                ? ExecuteCommandAndReturnOutput("where", "gcloud.cmd")
                : ExecuteCommandAndReturnOutput("which", "gcloud");

            var foundExecutable = result.Output.InfoLogs.FirstOrDefault();
            if (string.IsNullOrEmpty(foundExecutable))
            {
                throw new KubectlException("Could not find gcloud. Make sure gcloud is on the PATH.");
            }

            ExecutableLocation = foundExecutable.Trim();
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
                    throw new KubectlException("Failed to authenticate with gcloud. Key file is empty.");
                }

                log.Verbose("Authenticating to gcloud with key file");
                var bytes = Convert.FromBase64String(jsonKey);
                using (var keyFile = new TemporaryFile(Path.Combine(workingDirectory, "gcpJsonKey.json")))
                {
                    fileSystem.WriteAllBytes(keyFile.FilePath, bytes);
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

        public void ConfigureGkeKubeCtlAuthentication(IKubectl kubectlCli,
                                                      string gkeClusterName,
                                                      string region,
                                                      string zone,
                                                      string @namespace,
                                                      bool useClusterInternalIp)
        {
            log.Info($"Creating kubectl context to GKE Cluster called {gkeClusterName} (namespace {@namespace}) using a Google Cloud Account");

            var arguments = new List<string>(new[]
            {
                "container",
                "clusters",
                "get-credentials",
                gkeClusterName
            });

            if (useClusterInternalIp)
            {
                arguments.Add("--internal-ip");
            }

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
                throw new KubectlException("Either zone or region must be defined");
            }

            var result = ExecuteCommandAndLogOutput(new CommandLineInvocation(ExecutableLocation, arguments.ToArray()));
            result.VerifySuccess();
            kubectlCli.ExecuteCommandAndAssertSuccess("config", "set-context", "--current", $"--namespace={@namespace}");
        }
    }
}