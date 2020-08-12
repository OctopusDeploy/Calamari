using System;
using System.Threading.Tasks;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;
using Sashimi.Server.Contracts.Accounts;

namespace Octopus.Extensibility.Actions.Sashimi
{
    public interface IAccountDetailsEndpoint
    {
        string Method { get; }
        string Route { get; }
        string Description { get; }
        Task<IOctoResponseProvider> Respond(IOctoRequest request, string accountName, AccountDetails accountDetails);
    }
}