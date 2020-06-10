using System;
using System.Collections.Generic;
using FluentValidation;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Server.Contracts.Endpoints
{
    public interface IDeploymentTargetTypeProvider
    {
        DeploymentTargetType DeploymentTargetType { get; }
        Type DomainType { get; }
        Type ApiType { get; }

        IValidator Validator { get; }

        IEnumerable<AccountType> SupportedAccountTypes { get; }
    }
}