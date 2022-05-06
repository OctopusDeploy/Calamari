using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Common.Commands;
using Calamari.Common.Features.Discovery;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Commands.Discovery;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.Commands
{
    [Command(Name, Description = "Discovery Kubernetes cluster targets")]
    public class KubernetesDiscoveryCommand : Command
    {
        public const string Name = "kubernetes-target-discovery";
        public const string CreateKubernetesTargetServiceMessageName = "create-kubernetestarget";
        public const string ContextVariableName = "Octopus.TargetDiscovery.Context";
        
        readonly ILog log;
        readonly IVariables variables;
        readonly IKubernetesDiscovererFactory discovererFactory;

        public KubernetesDiscoveryCommand(ILog log,
            IVariables variables,
            IKubernetesDiscovererFactory discovererFactory)
        {
            this.log = log;
            this.variables = variables;
            this.discovererFactory = discovererFactory;
        }

        /// <returns>
        /// The Discovery Command always returns 0 to indicate success
        /// because a failure to discover targets should not cause a
        /// deployment process to fail. The one exception is if the
        /// json is malformed as this can only occur if there is
        /// a code issue and not from user misconfiguration.
        /// </returns>
        public override int Execute(string[] commandLineArguments)
        {
            if (!TryGetDiscoveryContextJson(out var json))
            {
                log.Warn($"Could not find target discovery context in variable {ContextVariableName}.");
                return ExitStatus.Success;
            }
            
            if (!TryGetAuthenticationContextTypeAndDiscoveryContextScope(json, out var type, out var scope))
                return ExitStatus.OtherError;

            if (!discovererFactory.TryGetKubernetesDiscoverer(type, out var discoverer))
            {
                log.Warn($"Authentication Context type of {type} is not currently supported for discovery.");
                return ExitStatus.Success;
            }

            var clusters = discoverer.DiscoverClusters(json).ToList();

            Log.Verbose($"Found {clusters.Count} candidate clusters.");
            var discoveredTargetCount = 0;
            foreach (var cluster in clusters)
            {
                var matchResult = scope.Match(cluster.Tags);
                if (matchResult.IsSuccess)
                {
                    discoveredTargetCount++;
                    Log.Info($"Discovered matching cluster: {cluster.Name}");
                    WriteTargetCreationServiceMessage(cluster, matchResult, scope);
                }
                else
                {
                    Log.Verbose($"Cluster {cluster.Name} does not match target requirements:");
                    foreach (var reason in matchResult.FailureReasons)
                    {
                        Log.Verbose($"- {reason}");
                    }
                }
            }

            Log.Info(discoveredTargetCount > 0
                ? $"{discoveredTargetCount} clusters found matching the given scope."
                : "Could not find any clusters matching the given scope.");

            return ExitStatus.Success;
        }

        void WriteTargetCreationServiceMessage(KubernetesCluster cluster, TargetMatchResult matchResult, TargetDiscoveryScope scope)
        {
            var parameters = new Dictionary<string, string> {
                { "name", cluster.Name },
                { "clusterName", cluster.Name },
                { "clusterUrl", "" },
                { "clusterResourceGroup", cluster.ResourceGroupName },
                { "clusterAdminLogin", "False" },
                { "namespace", "" },
                { "skipTlsVerification", "" },
                { "octopusAccountIdOrName", cluster.AccountId },
                { "octopusClientCertificateIdOrName", "" },
                { "octopusServerCertificateIdOrName", "" },
                { "octopusRoles", matchResult.Role },
                { "octopusDefaultWorkerPoolIdOrName", scope.WorkerPoolId },
                { "healthCheckContainerImageFeedIdOrName", "" },
                { "healthCheckContainerImage", "" },
                { "updateIfExisting", "True" },
                { "isDynamic", "True" },
                { "clusterProject", "" },
                { "clusterRegion", "" },
                { "clusterZone", "" },
                { "clusterImpersonateServiceAccount", "False" },
                { "clusterServiceAccountEmails", "" },
                { "clusterUseVmServiceAccount", "False" },
            };

            var serviceMessage = new ServiceMessage(
                CreateKubernetesTargetServiceMessageName,
                parameters.Where(p => p.Value != null)
                          .ToDictionary(p => p.Key, p => p.Value));
            
            log.WriteServiceMessage(serviceMessage);
        }

        bool TryGetDiscoveryContextJson(out string json)
        {
            return (json = variables.Get(ContextVariableName)) != null;
        }

        bool TryGetAuthenticationContextTypeAndDiscoveryContextScope(
            string contextJson, 
            out string type, 
            out TargetDiscoveryScope scope)
        {
            type = null;
            scope = null;
            try
            {
                var discoveryContext = JsonConvert
                    .DeserializeObject<TargetDiscoveryContext<AuthenticationType>>(contextJson);
                
                type = discoveryContext?.Authentication?.Type;
                scope = discoveryContext?.Scope;
                
                if (type != null && scope != null)
                    return true;
                
                log.Error($"Could not extract Type or Scope from {ContextVariableName}, the data is in the wrong format.");
                return false;
            }
            catch (JsonException ex)
            {
                log.Error($"Could not extract Type or Scope from {ContextVariableName}, the data is in the wrong format: {ex.Message}");
                return false;
            }
        }
        
        class AuthenticationType : ITargetDiscoveryAuthenticationDetails
        {
            public string Type { get; set; }
        }
    }
}