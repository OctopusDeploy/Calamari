using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

namespace Calamari.Kubernetes.ResourceStatus
{
    public enum ResourceAction
    {
        Created, Updated, Removed
    }
    
    public interface IResourceStatusChecker
    {
        void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, ICommandLineRunner commandLineRunner);
    }
    
    public class ResourceStatusChecker : IResourceStatusChecker
    {
        private readonly IResourceRetriever resourceRetriever;
        private readonly IServiceMessages serviceMessages;
        private readonly ILog log;
        private IDictionary<string, Resource> resources = new Dictionary<string, Resource>();
    
        // TODO remove this
        private int count = 20;
        
        public ResourceStatusChecker(IResourceRetriever resourceRetriever, IServiceMessages serviceMessages, ILog log)
        {
            this.resourceRetriever = resourceRetriever;
            this.serviceMessages = serviceMessages;
            this.log = log;
        }
        
        public void CheckStatusUntilCompletion(IEnumerable<ResourceIdentifier> resourceIdentifiers, ICommandLineRunner commandLineRunner)
        {
            var definedResources = resourceIdentifiers.ToList();
    
            resources = resourceRetriever
                .GetAllOwnedResources(definedResources, commandLineRunner)
                .ToDictionary(resource => resource.Uid, resource => resource);
    
            foreach (var (_, resource) in resources)
            {
                log.Info($"Found existing: {JsonConvert.SerializeObject(resource)}");
                serviceMessages.Update(resource);
            }
            
            while (!IsCompleted())
            {
                var newStatus = resourceRetriever
                    .GetAllOwnedResources(definedResources, commandLineRunner)
                    .ToDictionary(resource => resource.Uid, resource => resource);
    
                var diff = GetDiff(newStatus);
                resources = newStatus;
    
                foreach (var (action, resource) in diff)
                {
                    log.Info($"{(action == ResourceAction.Removed ? "Removed" : "")}{JsonConvert.SerializeObject(resource)}");

                    if (action == ResourceAction.Removed)
                    {
                        serviceMessages.Remove(resource);
                    }
                    else
                    {
                        serviceMessages.Update(resource);
                    }
                }
                
                Thread.Sleep(2000);
            }
        }
    
        private bool IsCompleted()
        {
            return resources.Count > 0 
                   && resources.All(resource => resource.Value.Status == Resources.ResourceStatus.Successful)
                || --count < 0;
        }
    
        private IEnumerable<(ResourceAction, Resource)> GetDiff(IDictionary<string, Resource> newStatus)
        {
            var diff = new List<(ResourceAction, Resource)>();
            foreach (var (uid, resource) in newStatus)
            {
                if (!resources.ContainsKey(uid))
                {
                    diff.Add((ResourceAction.Created, resource));
                }
                else if (resource.HasUpdate(resources[uid]))
                {
                    diff.Add((ResourceAction.Updated, resource));
                }
            }
    
            foreach (var (id, resource) in resources)
            {
                if (!newStatus.ContainsKey(id))
                {
                    diff.Add((ResourceAction.Removed, resource));
                }
            }
    
            return diff;
        }
    }
}