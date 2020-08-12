namespace Calamari.AzureWebApp
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                public static readonly string Environment = "Octopus.Action.Azure.Environment";
                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string ResourceGroupName = "Octopus.Action.Azure.ResourceGroupName";
                public static readonly string WebAppName = "Octopus.Action.Azure.WebAppName";
                public static readonly string WebAppSlot = "Octopus.Action.Azure.DeploymentSlot";
                public static readonly string RemoveAdditionalFiles = "Octopus.Action.Azure.RemoveAdditionalFiles";
                public static readonly string AppOffline = "Octopus.Action.Azure.AppOffline";
                public static readonly string PreserveAppData = "Octopus.Action.Azure.PreserveAppData";
                public static readonly string PreservePaths = "Octopus.Action.Azure.PreservePaths";
                public static readonly string PhysicalPath = "Octopus.Action.Azure.PhysicalPath";
                public static readonly string UseChecksum = "Octopus.Action.Azure.UseChecksum";
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