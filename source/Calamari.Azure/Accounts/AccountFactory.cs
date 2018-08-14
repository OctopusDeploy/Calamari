using System;
using Calamari.Shared;
using Octostache;

namespace Calamari.Azure.Accounts
{
    public static class AccountFactory
    {
        public static Account Create(VariableDictionary variables)
        {
            var accountType = variables.Get(SpecialVariables.Account.AccountType);

            switch (accountType)
            {
                case AzureAccountTypes.ServicePrincipalAccountType:
                    return new AzureServicePrincipalAccount(variables);
                case AzureAccountTypes.ManagementCertificateAccountType:
                    return new AzureAccount(variables);
            }
            throw new ApplicationException($"Unknown account type : {accountType}");
        }
    }
}