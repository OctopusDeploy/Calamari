using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService.Behaviors
{
    public class AzureAppServiceSettingsBehaviourFactory : IDeployBehaviour
    {
        readonly ILog log;

        public AzureAppServiceSettingsBehaviourFactory(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public Task Execute(RunningDeployment context)
        {
            return FeatureToggle.ModernAzureAppServiceSdkFeatureToggle.IsEnabled(context.Variables) 
                ? new AzureAppServiceSettingsBehaviour(log).Execute(context)
                : new LegacyAzureAppServiceSettingsBehaviour(log).Execute(context);   
        }
    }
}