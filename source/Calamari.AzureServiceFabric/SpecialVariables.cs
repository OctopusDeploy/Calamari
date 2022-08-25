namespace Calamari.AzureServiceFabric
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class ServiceFabric
            {
                public static readonly string ConnectionEndpoint = "Octopus.Action.ServiceFabric.ConnectionEndpoint";
                public static readonly string SecurityMode = "Octopus.Action.ServiceFabric.SecurityMode";
                public static readonly string ServerCertThumbprint = "Octopus.Action.ServiceFabric.ServerCertThumbprint";
                public static readonly string ClientCertVariable = "Octopus.Action.ServiceFabric.ClientCertVariable";
                public static readonly string CertificateStoreLocation = "Octopus.Action.ServiceFabric.CertificateStoreLocation";
                public static readonly string CertificateStoreName = "Octopus.Action.ServiceFabric.CertificateStoreName";
                public static readonly string AadUserCredentialUsername = "Octopus.Action.ServiceFabric.AadUserCredentialUsername";
                public static readonly string AadUserCredentialPassword = "Octopus.Action.ServiceFabric.AadUserCredentialPassword";
                public static readonly string CertificateFindType = "Octopus.Action.ServiceFabric.CertificateFindType";
                public static readonly string CertificateFindValueOverride = "Octopus.Action.ServiceFabric.CertificateFindValueOverride";
                public static readonly string AadCredentialType = "Octopus.Action.ServiceFabric.AadCredentialType";
                public static readonly string AadClientCredentialSecret = "Octopus.Action.ServiceFabric.AadClientCredentialSecret";
                public static readonly string PublishProfileFile = "Octopus.Action.ServiceFabric.PublishProfileFile";
                public static readonly string DeployOnly = "Octopus.Action.ServiceFabric.DeployOnly";
                public static readonly string UnregisterUnusedApplicationVersionsAfterUpgrade = "Octopus.Action.ServiceFabric.UnregisterUnusedApplicationVersionsAfterUpgrade";
                public static readonly string OverrideUpgradeBehavior = "Octopus.Action.ServiceFabric.OverrideUpgradeBehavior";
                public static readonly string OverwriteBehavior = "Octopus.Action.ServiceFabric.OverwriteBehavior";
                public static readonly string SkipPackageValidation = "Octopus.Action.ServiceFabric.SkipPackageValidation";
                public static readonly string CopyPackageTimeoutSec = "Octopus.Action.ServiceFabric.CopyPackageTimeoutSec";
                public static readonly string RegisterApplicationTypeTimeoutSec = "Octopus.Action.ServiceFabric.RegisterApplicationTypeTimeoutSec";
                public static readonly string LogExtractedApplicationPackage = "Octopus.Action.ServiceFabric.LogExtractedApplicationPackage";
            }
        }
    }
}