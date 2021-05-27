namespace Sashimi.Server.Contracts.Endpoints
{
    public enum CommunicationStyle
    {
        None = 0,

        /// <summary>
        /// Listening
        /// </summary>
        TentaclePassive = 1,

        /// <summary>
        /// Polling
        /// </summary>
        TentacleActive = 2,

        Ssh = 3,

        OfflineDrop = 4,

        AzureWebApp = 5,

        Ftp = 6,

        AzureCloudService = 7,

        AzureServiceFabricCluster = 8,

        Kubernetes = 9,
        
        StepPackage = 10
    }
}
