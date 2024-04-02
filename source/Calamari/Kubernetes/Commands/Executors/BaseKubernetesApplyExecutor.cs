#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.Commands.Executors
{
    abstract class BaseKubernetesApplyExecutor : IKubernetesApplyExecutor
    {
        readonly ILog log;
        readonly Kubectl kubectl;

        protected BaseKubernetesApplyExecutor(ILog log, Kubectl kubectl)
        {
            this.log = log;
            this.kubectl = kubectl;
        }
     
        public async Task<bool> Execute(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null)
        {
            try
            {
                var resourceIdentifiers = await ApplyAndGetResourceIdentifiers(deployment, appliedResourcesCallback);
                WriteResourcesToOutputVariables(resourceIdentifiers);
                return true;
            }
            catch (KubernetesApplyException)
            {
                return false;
            }
            catch (Exception e)
            {
                log.Error($"Deployment Failed due to exception: {e.Message}");
                return false;
            }
        }
        
        protected abstract Task<IEnumerable<ResourceIdentifier>> ApplyAndGetResourceIdentifiers(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null);
        
        protected void CheckResultForErrors(CommandResultWithOutput commandResult, string directory)
        {
            var directoryWithTrailingSlash = directory + Path.DirectorySeparatorChar;

            foreach (var message in commandResult.Output.Messages)
            {
                switch (message.Level)
                {
                    case Level.Info:
                        //No need to log as it's the output json from above.
                        break;
                    case Level.Error:
                        //Files in the error are shown with the full path in their batch directory,
                        //so we'll remove that for the user.
                        log.Error(message.Text.Replace($"{directoryWithTrailingSlash}", ""));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            
            if (commandResult.Result.ExitCode != 0)
            {
                throw new KubernetesApplyException();
            }
        }

        protected IEnumerable<ResourceIdentifier> ProcessKubectlCommandOutput(CommandResultWithOutput commandResult, string directory)
        {
            CheckResultForErrors(commandResult, directory);
            
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

                return lastResources.Select(r => r.ToResourceIdentifier());
            }
            catch
            {
                LogParsingError(outputJson);
            }
            
            return Enumerable.Empty<ResourceIdentifier>();
        }

        void WriteResourcesToOutputVariables(IEnumerable<ResourceIdentifier> resources)
        {
            foreach (var resource in resources)
            {
                try
                {
                    var result = kubectl.ExecuteCommandAndReturnOutput("get", resource.Kind, resource.Name,
                                                                       "-o", "json");

                    log.WriteServiceMessage(new ServiceMessage(ServiceMessageNames.SetVariable.Name, new Dictionary<string, string>
                    {
                        {ServiceMessageNames.SetVariable.NameAttribute, $"CustomResources({resource.Name})"},
                        {ServiceMessageNames.SetVariable.ValueAttribute, result.Output.InfoLogs.Join("\n")}
                    }));
                }
                catch
                {
                    log.Warn(
                             $"Could not save json for resource to output variable for '{resource.Kind}/{resource.Name}'");
                }
            }
        }
        
        void LogParsingError(string outputJson)
        {
            log.Error($"\"kubectl apply -o json\" returned invalid JSON:");
            log.Error("---------------------------");
            log.Error(outputJson);
            log.Error("---------------------------");
            log.Error("This can happen with older versions of kubectl. Please update to a recent version of kubectl.");
            log.Error("See https://github.com/kubernetes/kubernetes/issues/58834 for more details.");
            log.Error("Custom resources will not be saved as output variables.");

            throw new KubernetesApplyException();
        }
        
        protected class KubernetesApplyException : Exception
        {
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