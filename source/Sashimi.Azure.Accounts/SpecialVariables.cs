using System;

namespace Sashimi.Azure.Accounts
{
    class SpecialVariables
    {
        public static class Action
        {
            public static class Azure
            {
                public static readonly string SubscriptionId = "Octopus.Action.Azure.SubscriptionId";
                public static readonly string ClientId = "Octopus.Action.Azure.ClientId";
                public static readonly string TenantId = "Octopus.Action.Azure.TenantId";
                public static readonly string Password = "Octopus.Action.Azure.Password";
                public static readonly string Environment = "Octopus.Action.Azure.Environment";
                public static readonly string ResourceManagementEndPoint = "Octopus.Action.Azure.ResourceManagementEndPoint";
                public static readonly string ActiveDirectoryEndPoint = "Octopus.Action.Azure.ActiveDirectoryEndPoint";
            }
        }
    }
}