using System;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Azure.CloudServices.Accounts
{
    public static class AccountFactory
    {
        public static Account Create(VariableDictionary variables)
        {
            var accountType = variables.Get(SpecialVariables.Account.AccountType);

            switch (accountType)
            {
                case AzureAccountTypes.ManagementCertificateAccountType:
                    return new AzureAccount(variables);
            }
            throw new ApplicationException($"Unknown account type : {accountType}");
        }
    }
}