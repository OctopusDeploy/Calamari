using System;
using System.Collections.Generic;
using Octopus.Server.Extensibility.HostServices.Model.Feeds;
using Octostache;
using Sashimi.Server.Contracts.Accounts;
using Sashimi.Server.Contracts.Endpoints;

namespace Sashimi.Server.Contracts.ServiceMessages
{
    public interface ICreateTargetServiceMessageHandler : IServiceMessageHandler
    {
        Endpoint BuildEndpoint(IDictionary<string, string> messageProperties, 
                               VariableDictionary variables, 
                               Func<string, string> accountIdResolver, 
                               Func<string, string> certificateIdResolver, 
                               Func<string, string> workerPoolIdResolver, 
                               Func<string, AccountType> accountTypeResolver, 
                               Func<string, string> feedIdResolver);
    }
}