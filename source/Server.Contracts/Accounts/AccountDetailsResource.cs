using System;

namespace Sashimi.Server.Contracts.Accounts
{
    public abstract class AccountDetailsResource
    {
        public abstract AccountType AccountType { get; }
    }
}