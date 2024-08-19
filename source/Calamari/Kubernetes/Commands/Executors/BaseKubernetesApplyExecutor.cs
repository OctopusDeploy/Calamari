#if !NET40
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.Commands.Executors
{
    abstract class BaseKubernetesApplyExecutor : IKubernetesApplyExecutor
    {
        readonly ILog log;

        protected BaseKubernetesApplyExecutor(ILog log)
        {
            this.log = log;
        }
     
        public async Task<bool> Execute(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null)
        {
            try
            {
                await ApplyAndGetResourceIdentifiers(deployment, appliedResourcesCallback);
                return true;
            }
            catch (Exception e)
            {
                log.Error($"Deployment Failed due to exception: {e.Message}");
                return false;
            }
        }
        
        protected abstract Task<IEnumerable<ResourceIdentifier>> ApplyAndGetResourceIdentifiers(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null);

        protected IEnumerable<ResourceIdentifier> ProcessKubectlCommandOutput(RunningDeployment deployment, CommandResultWithOutput commandResult, string directory)
        {
            commandResult.LogErrorsWithSanitizedDirectory(log, directory);
            if (commandResult.Result.ExitCode != 0)
            {
                throw new KubectlException("Command Failed");
            }

            // If it did not error, the output should be valid json.
            var outputJson = commandResult.Output.InfoLogs.Join(Environment.NewLine);
            try
            {
                var token = JToken.Parse(outputJson);

                List<Resource> lastResources;
                if (token["kind"]?.ToString() != "List" ||
                    (lastResources = token["items"]?.ToObject<List<Resource>>()) == null)
                {
                    lastResources = new List<Resource> { token.ToObject<Resource>() };
                }

                var resources = lastResources.Select(r => r.ToResourceIdentifier()).ToList();

                if (resources.Any())
                {
                    log.Verbose("Created Resources:");
                    log.LogResources(resources);
                }

                return resources;
            }
            catch
            {
                LogParsingError(outputJson, deployment.Variables.GetFlag(SpecialVariables.PrintVerboseKubectlOutputOnError));
                throw new KubectlException("Log Parsing Error");
            }
        }
        
        void LogParsingError(string outputJson, bool logKubectlOutputOnError)
        {
            log.Error($"\"kubectl apply -o json\" returned invalid JSON");
            if (logKubectlOutputOnError)
            {
                log.Error("---------------------------");
                log.Error(outputJson);
                log.Error("---------------------------");
            }
            else
            {
                log.Error($"To get Octopus to log out the JSON string retrieved from kubectl, set Octopus Variable '{SpecialVariables.PrintVerboseKubectlOutputOnError}' to 'true'");
            }
            log.Error("This can happen with older versions of kubectl. Please update to a recent version of kubectl.");
            log.Error("See https://github.com/kubernetes/kubernetes/issues/58834 for more details.");
            log.Error("Custom resources will not be saved as output variables.");
        }

        class ResourceMetadata
        {
            public string Namespace { get; set; }
            public string Name { get; set; }
        }

        class Resource
        {
            public string Kind { get; set; }
            public ResourceMetadata Metadata { get; set; }

            public ResourceIdentifier ToResourceIdentifier()
            {
                return new ResourceIdentifier(Kind, Metadata.Name, Metadata.Namespace);
            }
        }
    }
}
#endif