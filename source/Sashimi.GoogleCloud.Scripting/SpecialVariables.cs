namespace Sashimi.GoogleCloud.Scripting
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class GoogleCloud
            {
                public static readonly string ActionTypeName = "Octopus.GoogleCloudScripting";
                
                public static readonly string AccountVariable = "Octopus.Action.GoogleCloudAccount.Variable";
                public static readonly string UseVMServiceAccount = "Octopus.Action.GoogleCloud.UseVMServiceAccount";
                public static readonly string ImpersonateServiceAccount = "Octopus.Action.GoogleCloud.ImpersonateServiceAccount";
                public static readonly string ServiceAccountEmails = "Octopus.Action.GoogleCloud.ServiceAccountEmails";
            }
        }
    }
}