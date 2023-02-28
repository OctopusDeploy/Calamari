using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.ResourceStatus
{
    public interface IServiceMessages
    {
        void Update(Resource resource);
        void Remove(Resource resource);
    }
    
    public class ServiceMessages : IServiceMessages
    {
        private readonly IVariables variables;
        private readonly ILog log;
        
        public ServiceMessages(IVariables variables, ILog log)
        {
            this.variables = variables;
            this.log = log;
        }
    
        public void Update(Resource resource) => UpdateOrRemove(resource, false);
    
        public void Remove(Resource resource) => UpdateOrRemove(resource, true);
    
        private void UpdateOrRemove(Resource resource, bool remove)
        {
            var parameters = new Dictionary<string, string>
            {
                {"data", remove ? "{}" : JsonConvert.SerializeObject(resource)},
                {"uid", resource.Uid},
                {"deploymentId", variables.Get("Octopus.Deployment.Id")},
                {"actionId", variables.Get("Octopus.Action.Id")}
            };
        
            var message = new Common.Plumbing.ServiceMessages.ServiceMessage("kubernetes-deployment-status-update", parameters);
            log.WriteServiceMessage(message);
        }
    }
}