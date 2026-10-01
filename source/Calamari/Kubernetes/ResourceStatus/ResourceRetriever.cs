using System;
using System.Collections.Generic;
using System.Linq;
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
        IEnumerable<ResourceRetrieverResult> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, IKubectl kubectl, Options options);
    }

    public class ResourceRetrieverResult
    {
        ResourceRetrieverResult(Resource value, string errorMessage)
        {
            Value = value;
            ErrorMessage = errorMessage;
        }

        public static ResourceRetrieverResult Success(Resource value) => new ResourceRetrieverResult(value, null);
        public static ResourceRetrieverResult Failure(string errorMessage) => new ResourceRetrieverResult(null, errorMessage);

        public Resource Value { get; }
        public bool IsSuccess => Value != null;
        public string ErrorMessage { get; }
    }

    public class ResourceRetriever : IResourceRetriever
    {
        readonly IKubectlGet kubectlGet;
        readonly ILog log;

        public ResourceRetriever(IKubectlGet kubectlGet, ILog log)
        {
            this.kubectlGet = kubectlGet;
            this.log = log;
        }

        /// <inheritdoc />
        public IEnumerable<ResourceRetrieverResult> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, IKubectl kubectl, Options options)
        {
            var childResourceCache = new Dictionary<(ResourceGroupVersionKind, string), ChildResourceLookup>();

            var results = resourceIdentifiers
                          .Select(identifier => GetResource(identifier, kubectl, options))
                          .ToList();

            foreach (var result in results.Where(r => r.IsSuccess))
            {
                var resource = result.Value;
                resource.UpdateChildren(GetChildrenResources(resource, kubectl, options, childResourceCache));
            }

            return results;
        }

        ResourceRetrieverResult GetResource(ResourceIdentifier resourceIdentifier, IKubectl kubectl, Options options)
        {
            var result = kubectlGet.Resource(resourceIdentifier, kubectl);
            LogKubectlErrorIfFailed(result, options, log);

            if (!result.HasOutput)
                return ResourceRetrieverResult.Failure($"Failed to get resource {resourceIdentifier.Name} in namespace {resourceIdentifier.Namespace}");

            var parseResult = TryParse(ResourceFactory.FromJson, result, options);
            return !parseResult.IsSuccess ? ResourceRetrieverResult.Failure(parseResult.ErrorMessage) : ResourceRetrieverResult.Success(parseResult.Value);
        }

        IEnumerable<Resource> GetChildrenResources(Resource parentResource, IKubectl kubectl, Options options,
            Dictionary<(ResourceGroupVersionKind, string), ChildResourceLookup> childResourceCache)
        {
            var childGvk = parentResource.ChildGroupVersionKind;
            if (childGvk is null) return Enumerable.Empty<Resource>();

            var lookup = GetChildResourceLookupCached(childGvk, parentResource.Namespace, kubectl, options, childResourceCache);
            var children = lookup.ForOwner(parentResource.Uid);

            foreach (var child in children)
            {
                // the lookup is shared between parents, so resolve each child's children once
                if (child.Children == null)
                {
                    child.UpdateChildren(GetChildrenResources(child, kubectl, options, childResourceCache));
                }
            }

            return children;
        }

        // only cache the parsed results, or you'll cause bankruptcy given post-ai memory costs
        ChildResourceLookup GetChildResourceLookupCached(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl, Options options,
            Dictionary<(ResourceGroupVersionKind, string), ChildResourceLookup> childResourceCache)
        {
            var cacheKey = (groupVersionKind, @namespace);
            if (childResourceCache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var lookup = BuildChildResourceLookup(groupVersionKind, @namespace, kubectl, options);
            childResourceCache[cacheKey] = lookup;
            return lookup;
        }

        ChildResourceLookup BuildChildResourceLookup(ResourceGroupVersionKind groupVersionKind, string @namespace, IKubectl kubectl, Options options)
        {
            var result = kubectlGet.AllResources(groupVersionKind, @namespace, kubectl);
            LogKubectlErrorIfFailed(result, options, log);

            if (!result.HasOutput)
            {
                // Child resources are ignored for determining deployment success.
                log.Verbose($"Failed to get child resources of type {groupVersionKind.Kind} in namespace {@namespace}");
                return ChildResourceLookup.Empty;
            }

            var parseResult = TryParse(ResourceFactory.FromListJson, result, options);
            if (!parseResult.IsSuccess)
            {
                // Child resources are ignored for determining deployment success.
                log.Verbose($"Failed to parse child resources of type {groupVersionKind.Kind} in namespace {@namespace}");
                log.Verbose(parseResult.ErrorMessage);
                return ChildResourceLookup.Empty;
            }

            return ChildResourceLookup.From(parseResult.Value);
        }

        static ParseResult<T> TryParse<T>(Func<string, Options, T> function, KubectlGetResult getResult, Options options) where T : class
        {
            try
            {
                return ParseResult<T>.Success(function(getResult.ResourceJson, options));
            }
            catch (JsonException)
            {
                return ParseResult<T>.Failure(GetJsonStringError(getResult.ResourceJson, options));
            }
        }

        static string GetJsonStringError(string jsonString, Options options)
        {
            if (!options.PrintVerboseKubectlOutputOnError)
                return $"Failed to parse JSON, to get Octopus to log out the JSON string retrieved from kubectl, set Octopus Variable '{SpecialVariables.PrintVerboseKubectlOutputOnError}' to 'true'";

            var message = "";
            message += "Failed to parse JSON:\n";
            message += "---------------------------\n";
            message += jsonString + "\n";
            message += "---------------------------\n";
            return message;
        }

        static void LogKubectlErrorIfFailed(KubectlGetResult getResult, Options options, ILog log)
        {
            if (getResult.ExitCode == 0)
                return;

            if (!options.PrintVerboseKubectlOutputOnError)
            {
                log.Verbose($"kubectl error, to get Octopus to the log the full error, set Octopus Variable '{SpecialVariables.PrintVerboseKubectlOutputOnError}' to 'true'");
                return;
            }

            log.Verbose($"kubectl failed with exit code: {getResult.ExitCode}");
            log.Verbose("---------------------------");

            foreach (var line in getResult.RawOutput)
            {
                log.Verbose(line);
            }

            log.Verbose("---------------------------");
        }

        class ChildResourceLookup
        {
            public static readonly ChildResourceLookup Empty = new ChildResourceLookup(new Dictionary<string, List<Resource>>());

            readonly Dictionary<string, List<Resource>> byOwnerUid;

            ChildResourceLookup(Dictionary<string, List<Resource>> byOwnerUid)
            {
                this.byOwnerUid = byOwnerUid;
            }

            public static ChildResourceLookup From(IEnumerable<Resource> resources)
            {
                var byOwnerUid = new Dictionary<string, List<Resource>>();

                foreach (var resource in resources)
                {
                    foreach (var ownerUid in resource.OwnerUids ?? Enumerable.Empty<string>())
                    {
                        if (ownerUid.IsNullOrEmpty())
                            continue;

                        if (!byOwnerUid.TryGetValue(ownerUid, out var owned))
                        {
                            owned = new List<Resource>();
                            byOwnerUid[ownerUid] = owned;
                        }

                        owned.Add(resource);
                    }
                }

                return new ChildResourceLookup(byOwnerUid);
            }

            public IReadOnlyList<Resource> ForOwner(string ownerUid)
                => !ownerUid.IsNullOrEmpty() && byOwnerUid.TryGetValue(ownerUid, out var owned)
                    ? owned
                    : Array.Empty<Resource>();
        }

        class ParseResult<T> where T : class
        {
            ParseResult(T value, string errorMessage)
            {
                Value = value;
                ErrorMessage = errorMessage;
            }

            public static ParseResult<T> Success(T value) => new ParseResult<T>(value, null);
            public static ParseResult<T> Failure(string errorMessage) => new ParseResult<T>(null, errorMessage);

            public T Value { get; }
            public bool IsSuccess => Value != null;
            public string ErrorMessage { get; }
        }
    }
}
