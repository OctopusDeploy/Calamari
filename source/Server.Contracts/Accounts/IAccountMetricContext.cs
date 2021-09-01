using System;
using System.Collections.Generic;

namespace Sashimi.Server.Contracts.Accounts
{
    public interface IAccountMetricContext
    {
        IEnumerable<T> GetAccountDetails<T>() where T : AccountDetails;
    }
}