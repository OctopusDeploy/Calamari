namespace Calamari.AzureScripting
{
    public static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                public static readonly string Environment = "Octopus.Action.Azure.Environment";
                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string ClientId = "Octopus.Action.Azure.ClientId";
                public static readonly string TenantId = "Octopus.Action.Azure.TenantId";
                public static readonly string Password = "Octopus.Action.Azure.Password";
                public static readonly string Jwt = "Octopus.OpenIdConnect.Jwt";
                public static readonly string StorageAccountName = "Octopus.Action.Azure.StorageAccountName";
                public static readonly string CertificateBytes = "Octopus.Action.Azure.CertificateBytes";
                public static readonly string CertificateThumbprint = "Octopus.Action.Azure.CertificateThumbprint";
                public static readonly string ExtensionsDirectory = "Octopus.Action.Azure.ExtensionsDirectory";
            }
        }

        public static class Account
        {
            public const string Name = "Octopus.Account.Name";
            public const string AccountType = "Octopus.Account.AccountType";
            public const string Username = "Octopus.Account.Username";
            public const string Password = "Octopus.Account.Password";
            public const string Token = "Octopus.Account.Token";
        }
    }
}