namespace Sashimi.Server.Contracts.Endpoints
{
    public class DeploymentActionContainerResource
    {
        public string? Image { get; set; }
        public string? FeedId { get; set; }
    }
}