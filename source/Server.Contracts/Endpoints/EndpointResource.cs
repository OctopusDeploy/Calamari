using Octopus.Server.MessageContracts;

namespace Sashimi.Server.Contracts.Endpoints
{
    public abstract class EndpointResource : Resource
    {
        public abstract CommunicationStyle CommunicationStyle { get; }
    }
}