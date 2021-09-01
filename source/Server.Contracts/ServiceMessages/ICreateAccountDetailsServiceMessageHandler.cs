using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Server.Contracts.ServiceMessages
{
    public interface ICreateAccountDetailsServiceMessageHandler : IServiceMessageHandler
    {
        AccountDetails CreateAccountDetails(IDictionary<string, string> properties, ITaskLog taskLog);
    }
}