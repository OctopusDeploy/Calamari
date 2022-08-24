using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;

namespace Calamari.Common.Features.Discovery
{
    public abstract class KubernetesDiscovererBase : IKubernetesDiscoverer
    {
        protected readonly ILog Log;

        protected KubernetesDiscovererBase(ILog log)
        {
            this.Log = log;
        }

        protected bool TryGetDiscoveryContext<TAuthenticationDetails>(string json, 
            [NotNullWhen(returnValue: true)] out TAuthenticationDetails? authenticationDetails,
            out string? workerPoolId) where TAuthenticationDetails : class, ITargetDiscoveryAuthenticationDetails
        {
            authenticationDetails = null;
            workerPoolId = null;
            try
            {
                var discoveryContext =
                    JsonConvert.DeserializeObject<TargetDiscoveryContext<TAuthenticationDetails>>(json);

                if (discoveryContext == null)
                {
                    LogDeserialisationError();
                    return false;
                }

                if (discoveryContext.Authentication == null)
                {
                    LogDeserialisationError("authentication details");
                    return false;
                }

                if (discoveryContext.Scope == null)
                {
                    LogDeserialisationError("scope");
                    return false;
                }

                authenticationDetails = discoveryContext.Authentication;
                workerPoolId = discoveryContext.Scope.WorkerPoolId;
                return true;
            }
            catch (Exception ex)
            {
                LogDeserialisationError(exception: ex);
                return false;
            }
        }
        void LogDeserialisationError(string? detail = null, Exception? exception = null)
        {
            Log.Warn("Target discovery context is in the wrong format, please contact Octopus Support.");                    
            Log.Verbose($"Unable to deserialise {(detail == null ? $"{detail} in " : "")} Target Discovery Context" +
                $"{(exception == null ? " but no exception was thrown" : "")}, aborting discovery" +
                $"{(exception == null ? "." : $":{exception}")}.");
        }

        public abstract string Type { get; }
        public abstract IEnumerable<KubernetesCluster> DiscoverClusters(string contextJson);
    }
}