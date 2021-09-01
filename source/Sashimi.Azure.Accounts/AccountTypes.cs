using System;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Azure.Accounts
{
    public static class AccountTypes
    {
        public static readonly AccountType AzureServicePrincipalAccountType = new("AzureServicePrincipal");
    }
}