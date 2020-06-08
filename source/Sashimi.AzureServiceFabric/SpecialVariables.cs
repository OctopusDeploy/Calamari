namespace Sashimi.AzureServiceFabric
{
    //TODO: This is duplicated from Server while we sort out a way for Sashimi to contribute special variables.
    static class SpecialVariables
    {
        public static class Action
        {
            public static class ServiceFabric
            {
                #region Connection variables

                public static readonly string ConnectionEndpoint = "Octopus.Action.ServiceFabric.ConnectionEndpoint";

                public static readonly string SecurityMode = "Octopus.Action.ServiceFabric.SecurityMode";

                public static readonly string ServerCertThumbprint = "Octopus.Action.ServiceFabric.ServerCertThumbprint";

                public static readonly string ClientCertVariable = "Octopus.Action.ServiceFabric.ClientCertVariable";

                public static readonly string AadCredentialType = "Octopus.Action.ServiceFabric.AadCredentialType";

                public static readonly string AadClientCredentialSecret = "Octopus.Action.ServiceFabric.AadClientCredentialSecret";

                public static readonly string AadUserCredentialUsername = "Octopus.Action.ServiceFabric.AadUserCredentialUsername";

                public static readonly string AadUserCredentialPassword = "Octopus.Action.ServiceFabric.AadUserCredentialPassword";

                #endregion

                #region Certificate override variables

                public static readonly string CertificateStoreLocation = "Octopus.Action.ServiceFabric.CertificateStoreLocation";

                public static readonly string CertificateStoreName = "Octopus.Action.ServiceFabric.CertificateStoreName";

                #endregion
            }
        }
    }
}