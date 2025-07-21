using System.Threading.Tasks;
using Calamari.Azure.AppServices;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.AzureWebApp
{
    public interface IWebDeploymentExecutor
    {
        Task ExecuteDeployment(RunningDeployment deployment,
                               AzureTargetSite targetSite,
                               IVariables variables,
                               WebDeployPublishSettings publishSettings);
    }
}