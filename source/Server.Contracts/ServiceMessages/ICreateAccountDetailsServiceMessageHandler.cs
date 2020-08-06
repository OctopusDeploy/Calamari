using System.Collections.Generic;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Server.Contracts.ServiceMessages
{
    public interface ICreateAccountDetailsServiceMessageHandler : IServiceMessageHandler
    {
        AccountDetails CreateAccountDetails(IDictionary<string, string> properties);
    }
}