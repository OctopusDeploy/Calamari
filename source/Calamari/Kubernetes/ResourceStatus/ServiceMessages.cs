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
    }
    
    public class ServiceMessages : IServiceMessages
    {
        private readonly ILog log;
        
        public ServiceMessages(ILog log)
        {
            this.log = log;
        }

        // TODO: This needs to be changed when adopting the database solution
        public void Update(Resource resource)
        {
            var parameters = new Dictionary<string, string>
            {
                {"type", "k8s-status"},
                {"data", JsonConvert.SerializeObject(resource)}
            };
        
            var message = new ServiceMessage("logData", parameters);
            log.WriteServiceMessage(message);
        }
    }
}