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
        private readonly IKubectlGet kubectlGet;
        readonly ILog log;

        public ResourceRetriever(IKubectlGet kubectlGet, ILog log)
        {
            this.kubectlGet = kubectlGet;
            this.log = log;
        }

        /// <inheritdoc />
        public IEnumerable<ResourceRetrieverResult> GetAllOwnedResources(IEnumerable<ResourceIdentifier> resourceIdentifiers, IKubectl kubectl, Options options)
        {
            var results = resourceIdentifiers
                .Select(identifier => GetResource(identifier, kubectl, options))
                .ToList();

            foreach (var result in results.Where(r => r.IsSuccess))
            {
                var resource = result.Value;
                resource.UpdateChildren(GetChildrenResources(resource, kubectl, options));
            }

            return results;
        }

        private ResourceRetrieverResult GetResource(ResourceIdentifier resourceIdentifier, IKubectl kubectl, Options options)
        {
            var result = kubectlGet.Resource(resourceIdentifier, kubectl);
            
            if (result.RawOutput.IsNullOrEmpty()) 
                return ResourceRetrieverResult.Failure($"Failed to get resource {resourceIdentifier.Name} in namespace {resourceIdentifier.Namespace}");

            var parseResult = TryParse(ResourceFactory.FromJson, result, options);
            return !parseResult.IsSuccess ? ResourceRetrieverResult.Failure(parseResult.ErrorMessage) : ResourceRetrieverResult.Success(parseResult.Value);
        }

        private IEnumerable<Resource> GetChildrenResources(Resource parentResource, IKubectl kubectl, Options options)
        {
            var childGvk = parentResource.ChildGroupVersionKind;
            if (childGvk is null) return Enumerable.Empty<Resource>();

            var result = kubectlGet.AllResources(childGvk, parentResource.Namespace, kubectl);
            if (result.RawOutput.IsNullOrEmpty())
            {
                // Child resources are ignored for determining deployment success.
                log.Verbose($"Failed to get child resources for {parentResource.Name} in namespace {parentResource.Namespace}");
                return Enumerable.Empty<Resource>();
            }
            
            var parseResult = TryParse(ResourceFactory.FromListJson, result, options);
            if (!parseResult.IsSuccess)
            {
                // Child resources are ignored for determining deployment success.
                log.Verbose($"Failed to parse child resources for {parentResource.Name} in namespace {parentResource.Namespace}");
                log.Verbose(parseResult.ErrorMessage);
                return Enumerable.Empty<Resource>();
            };
            
            var resources = parseResult.Value;
            return resources.Where(resource => resource.OwnerUids.Contains(parentResource.Uid))
                            .Select(child =>
                            {
                                child.UpdateChildren(GetChildrenResources(child, kubectl, options));
                                return child;
                            }).ToList();
        }
        
        static ParseResult<T> TryParse<T>(Func<string, Options, T> function, KubectlGetResult getResult, Options options) where T : class
        {
            try
            {
                return ParseResult<T>.Success(function(getResult.ResourceJson, options));
            }
            catch (JsonException)
            {
                return ParseResult<T>.Failure(GetJsonStringError(getResult.ResourceJson, getResult, options));
            }
        }

        static string GetJsonStringError(string jsonString, KubectlGetResult getResult, Options options)
        {
            if (!options.PrintVerboseKubectlOutputOnError)
                return $"Failed to parse JSON, to get Octopus to log out the JSON string retrieved from kubectl, set Octopus Variable '{SpecialVariables.PrintVerboseKubectlOutputOnError}' to 'true'";
            
            var message = "";
            message += "Failed to parse JSON:\n";
            message += "---------------------------\n";
            message += jsonString + "\n";
            message += "---------------------------\n";
            message += "Full command output:\n";
            message += "---------------------------\n";
            message += getResult.RawOutput.Join("\n") + "\n";
            message += "---------------------------\n";
            return message;
        }
        
        class ParseResult<T> where T : class 
        {
            ParseResult (T value, string errorMessage)
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