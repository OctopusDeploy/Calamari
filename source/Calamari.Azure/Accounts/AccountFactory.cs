using System;
using Calamari.Deployment;
using Octostache;

namespace Calamari.Azure.Accounts
{
    public static class AccountFactory
    {
        public static AzureServicePrincipalAccount Create(VariableDictionary variables)
        {
            var accountType = variables.Get(SpecialVariables.Account.AccountType);

            switch (accountType)
            {
                case AzureAccountTypes.ServicePrincipalAccountType:
                    return new AzureServicePrincipalAccount(variables);
            }
            throw new ApplicationException($"Unknown or unsupported account type : {accountType}");
        }
    }
}