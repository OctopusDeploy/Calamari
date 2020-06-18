using System;
using System.Collections.Generic;
using FluentValidation;
using Octopus.Server.Extensibility.Extensions.Mappings;

namespace Sashimi.Server.Contracts.Accounts
{
    public interface IAccountTypeProvider: IContributeMappings, IServiceMessageHandler
    {
        AccountDetails CreateViaServiceMessage(IDictionary<string, string> properties);
        AccountType AccountType { get; }
        Type ModelType { get; }
        Type ApiType { get; }
        IValidator Validator { get; }
        IVerifyAccount Verifier { get; }
        IEnumerable<(string key, object value)> GetFeatureUsage(IAccountMetricContext context);
    }
}