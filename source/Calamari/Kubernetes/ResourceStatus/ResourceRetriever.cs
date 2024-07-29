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
            var result = kubectlGet.Resource(resourceIdentifier.Kind, resourceIdentifier.Name, resourceIdentifier.Namespace, kubectl);
            
            return result.InfoLogs.IsNullOrEmpty() ? null : TryParse(ResourceFactory.FromJson, result, options);
        }

        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, IKubectl kubectl, Options options)
        {
            var childKind = parentResource.ChildKind;
            if (string.IsNullOrEmpty(childKind))
            {
                return Enumerable.Empty<Resource>();
            }

            var result = kubectlGet.AllResources(childKind, parentResource.Namespace, kubectl);

            var resources = TryParse(ResourceFactory.FromListJson, result, options);
            return resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid))
                            .Select(child =>
                            {
                                child.UpdateChildren(GetChildrenResources(child, kubectl, options));
                                return child;
                            }).ToList();
        }

        T TryParse<T>(Func<string, Options, T> function, ICommandOutput commandOutput, Options options)
        {
            var jsonString = commandOutput.InfoLogs.Join(string.Empty);
            try
            {
                
                return function(jsonString, options);
            }
            catch (JsonException)
            {
                LogJsonStringError(jsonString, commandOutput, options);
                throw;
            }
        }

        void LogJsonStringError(string jsonString, ICommandOutput commandOutput, Options options)
        {
            if (options.PrintVerboseKubectlOutputOnError)
            {
                log.Error("Failed to parse JSON:");
                log.Error("---------------------------");
                log.Error(jsonString);
                log.Error("---------------------------");
                log.Error("Full command output:");
                log.Error("---------------------------");
                foreach (var msg in commandOutput.Messages)
                {
                    log.Error($"{msg.Level}: {msg.Text}");
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