using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Azure.AppServices;
using Calamari.AzureWebApp.Integration.Websites.Publishing;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureWebApp
{
    class AzureWebAppBehaviour : IDeployBehaviour
    {
        readonly ILog log;
        readonly ResourceManagerPublishProfileProvider resourceManagerPublishProfileProvider;
        readonly IWebDeploymentExecutor webDeploymentExecutor;

        public AzureWebAppBehaviour(ILog log, ResourceManagerPublishProfileProvider resourceManagerPublishProfileProvider, IWebDeploymentExecutor webDeploymentExecutor)
        {
            this.log = log;
            this.resourceManagerPublishProfileProvider = resourceManagerPublishProfileProvider;
            this.webDeploymentExecutor = webDeploymentExecutor;
        }

        public bool IsEnabled(RunningDeployment context) => true;

        public async Task Execute(RunningDeployment deployment)
        {
            var variables = deployment.Variables;
            var account = AzureAccountFactory.Create(variables);

            //it's possible to have an empty resource group name here, so we allow empty resource group names
            var targetSite = AzureTargetSite.Create(account, variables, log, true);

            var resourceGroupText = string.IsNullOrEmpty(targetSite.ResourceGroupName) ? string.Empty : $" in Resource Group '{targetSite.ResourceGroupName}'";
            var slotText = targetSite.HasSlot ? $", deployment slot '{targetSite.Slot}'" : string.Empty;

            var armClient = account.CreateArmClient();

            // We will skip checking if SCM is enabled when a resource group is not provided as it's not possible, and authentication may still be valid
            if (!string.IsNullOrEmpty(targetSite.ResourceGroupName))
            {
                var isCurrentScmBasicAuthPublishingEnabled = await armClient.IsScmPublishEnabled(targetSite);
                if (!isCurrentScmBasicAuthPublishingEnabled)
                {
                    log.Error($"The 'SCM Basic Auth Publishing Credentials' configuration is disabled on '{targetSite.Site}'-{slotText}. To deploy Web Apps with SCM disabled, please use the 'Deploy an Azure App Service' step.");
                    throw new CommandException($"The 'SCM Basic Auth Publishing Credentials' is disabled on target '{targetSite.Site}'{slotText}");
                }
            }
            else
            {
                log.Warn($"No Resource Group Name was provided. Checking 'SCM Basic Auth Publishing Credentials' configuration will be skipped. If authentication fails, ensure 'SCM Basic Auth Publishing Credentials' is enabled on '{targetSite.Site}'-{slotText}. To deploy Web Apps with SCM disabled, please use the 'Deploy an Azure App Service' step.");
            }

            log.Info($"Deploying to Azure WebApp  '{targetSite.Site}'{slotText}{resourceGroupText}, using subscription-id '{targetSite.SubscriptionId}'");
            var publishSettings = await resourceManagerPublishProfileProvider.GetPublishProperties(account, targetSite);

            RemoteCertificateValidationCallback originalServerCertificateValidationCallback = null;
            try
            {
                originalServerCertificateValidationCallback = ServicePointManager.ServerCertificateValidationCallback;
                ServicePointManager.ServerCertificateValidationCallback = WrapperForServerCertificateValidationCallback;

                await webDeploymentExecutor.ExecuteDeployment(deployment, targetSite, variables, publishSettings);
            }
            finally
            {
                ServicePointManager.ServerCertificateValidationCallback = originalServerCertificateValidationCallback;
            }
        }

        bool WrapperForServerCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            switch (sslPolicyErrors)
            {
                case SslPolicyErrors.None:
                    return true;
                case SslPolicyErrors.RemoteCertificateNameMismatch:
                    log.Error(
                              $"A certificate mismatch occurred. We have had reports previously of Azure using incorrect certificates for some Web App SCM sites, which seem to related to a known issue, a possible fix is documented in {log.FormatLink("https://g.octopushq.com/CertificateMismatch")}.");
                    break;
            }

            return false;
        }
    }
}