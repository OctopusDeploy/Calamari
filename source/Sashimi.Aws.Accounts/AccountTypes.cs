using System;
using Sashimi.Server.Contracts.Accounts;

namespace Sashimi.Aws.Accounts
{
    public static class AccountTypes
    {
        public static readonly AccountType AmazonWebServicesAccountType = new("AmazonWebServicesAccount");
    }
}