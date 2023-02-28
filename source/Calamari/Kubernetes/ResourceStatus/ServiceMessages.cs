using System.Collections.Generic;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json;

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
                {"type", "k8s-status"},
                {"data", remove ? "{}" : JsonConvert.SerializeObject(resource)}
            };
        
            var message = new ServiceMessage("logData", parameters);
            log.WriteServiceMessage(message);
        }
    }
}