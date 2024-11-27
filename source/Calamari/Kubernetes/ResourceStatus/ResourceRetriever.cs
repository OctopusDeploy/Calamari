using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.ResourceStatus
{
    /// <summary>
    /// Retrieves resources information from a kubernetes cluster
    /// </summary>
    public interface IResourceRetriever
    {
        /// <summary>
        /// Gets the resources identified by the resourceIdentifiers and all their owned resources
        /// </summary>
        IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, IKubectl kubectl, Options options);
    }

    public class ResourceRetriever : IResourceRetriever
    {
        private readonly IKubectlGet kubectlGet;
        readonly ILog log;

        public ResourceRetriever(IKubectlGet kubectlGet, ILog log)
        {
            this.kubectlGet = kubectlGet;
            this.log = log;
        }

        /// <inheritdoc />
        public IEnumerable<Resource> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, IKubectl kubectl, Options options)
        {
            var resources = resourceIdentifiers
                .Select(identifier => GetResource(identifier, kubectl, options))
                .Where(resource => resource != null)
                .ToList();

            foreach (var resource in resources)
            {
                resource.UpdateChildren(GetChildrenResources(resource, kubectl, options));
            }

            return resources;
        }

        private Resource GetResource(ResourceIdentifier resourceIdentifier, IKubectl kubectl, Options options)
        {
            var result = kubectlGet.Resource(resourceIdentifier, kubectl);
            
            return result.RawOutput.IsNullOrEmpty() ? null : TryParse(ResourceFactory.FromJson, result, options);
        }

        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, IKubectl kubectl, Options options)
        {
            var childGvk = parentResource.ChildGroupVersionKind;
            if (childGvk is null)
            {
                return Enumerable.Empty<Resource>();
            }

            var result = kubectlGet.AllResources(childGvk, parentResource.Namespace, kubectl);

            var resources = TryParse(ResourceFactory.FromListJson, result, options);
            return resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid))
                            .Select(child =>
                            {
                                child.UpdateChildren(GetChildrenResources(child, kubectl, options));
                                return child;
                            }).ToList();
        }

        T TryParse<T>(Func<string, Options, T> function, KubectlGetResult getResult, Options options)
        {
            try
            {
                
                return function(getResult.ResourceJson, options);
            }
            catch (JsonException)
            {
                LogJsonStringError(getResult.ResourceJson, getResult, options);
                throw;
            }
        }

        void LogJsonStringError(string jsonString, KubectlGetResult getResult, Options options)
        {
            if (options.PrintVerboseKubectlOutputOnError)
            {
                log.Error("Failed to parse JSON:");
                log.Error("---------------------------");
                log.Error(jsonString);
                log.Error("---------------------------");
                log.Error("Full command output:");
                log.Error("---------------------------");
                foreach (var msg in getResult.RawOutput)
                {
                    log.Error(msg);
                }
                log.Error("---------------------------");
            }
            else
            {
                log.Error($"Failed to parse JSON, to get Octopus to log out the JSON string retrieved from kubectl, set Octopus Variable '{SpecialVariables.PrintVerboseKubectlOutputOnError}' to 'true'");
            }
        }
    }
}