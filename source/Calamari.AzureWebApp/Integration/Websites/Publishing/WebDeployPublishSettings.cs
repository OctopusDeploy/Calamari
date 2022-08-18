namespace Calamari.AzureWebApp.Integration.Websites.Publishing
{
    class WebDeployPublishSettings {
        /// <summary>
        /// The deployment site name may be different for web deploy especially when using slots. This may
        /// end up as something like slotname___sitename and is what needs to be provided to web deploy.
        /// </summary>
        public string DeploymentSite { get; }
        public SitePublishProfile PublishProfile { get; }

        public WebDeployPublishSettings(string deploymentSite, SitePublishProfile publishProfile)
        {
            DeploymentSite = deploymentSite;
            PublishProfile = publishProfile;
        }
    }
}