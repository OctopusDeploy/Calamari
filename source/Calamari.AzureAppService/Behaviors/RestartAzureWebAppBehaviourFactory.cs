using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService.Behaviors
{
    public class RestartAzureWebAppBehaviourFactory : IDeployBehaviour
    {
        readonly ILog log;

        public RestartAzureWebAppBehaviourFactory(ILog log)
        {
            this.log = log;
        }
        
        public bool IsEnabled(RunningDeployment context) => true;

        public Task Execute(RunningDeployment context)
        {
            return FeatureToggle.UseModernAzureAppServiceSdkFeatureToggle.IsEnabled(context.Variables) 
                ? new RestartAzureWebAppBehaviour(log).Execute(context)
                : new LegacyRestartAzureWebAppBehaviour(log).Execute(context);   
        }
    }
}