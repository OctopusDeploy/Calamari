using System;
using System.Collections.Generic;
using FluentValidation;
using Octopus.Server.Extensibility.Extensions.Mappings;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Server.Contracts.Accounts
{
    public interface IAccountTypeProvider : IContributeMappings
    {
        AccountType AccountType { get; }
        Type ModelType { get; }
        Type ApiType { get; }
        IValidator Validator { get; }
        IVerifyAccount Verifier { get; }
        ICreateAccountDetailsServiceMessageHandler? CreateAccountDetailsServiceMessageHandler { get; }
        IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context);
    }
}