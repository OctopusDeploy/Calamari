using System;

namespace Calamari.Testing.LogParser
{
    public class ScriptServiceMessageNames
    {
        public static class SetVariable
        {
            public const string Name = "setVariable";
            public const string NameAttribute = "name";
            public const string ValueAttribute = "value";
            public const string SensitiveAttribute = "sensitive";
        }

        public static class StdOutBehaviour
        {
            public const string Ignore = "stdout-ignore";
            public const string Error = "stdout-error";
            public const string Default = "stdout-default";
            public const string Warning = "stdout-warning";
            public const string Verbose = "stdout-verbose";
            public const string Highlight = "stdout-highlight";
            public const string Wait = "stdout-wait";
        }

        public static class StdErrBehavior
        {
            public const string Ignore = "stderr-ignore";
            public const string Progress = "stderr-progress";
            public const string Error = "stderr-error";
            public const string Default = "stderr-default";
        }

        public static class Progress
        {
            public const string Name = "progress";
            public const string Percentage = "percentage";
            public const string Message = "message";
        }

        public static class CreateArtifact
        {
            public const string Name = "createArtifact";
            public const string PathAttribute = "path";
            public const string NameAttribute = "name";
            public const string LengthAttribute = "length";
        }

        public static class ResultMessage
        {
            public const string Name = "resultMessage";
            public const string MessageAttribute = "message";
        }

        public static class CalamariFoundPackage
        {
            public const string Name = "calamari-found-package";
        }

        public static class FoundPackage
        {
            public const string Name = "foundPackage";
            public const string IdAttribute = "id";
            public const string VersionAttribute = "version";
            public const string VersionFormat = "versionFormat";
            public const string HashAttribute = "hash";
            public const string RemotePathAttribute = "remotePath";
            public const string FileExtensionAttribute = "fileExtension";
        }

        public static class PackageDeltaVerification
        {
            public const string Name = "deltaVerification";
            public const string RemotePathAttribute = "remotePath";
            public const string HashAttribute = "hash";
            public const string SizeAttribute = "size";
            public const string Error = "error";
        }

        public static class ScriptOutputActions
        {
            public const string AccountIdOrNameAttribute = "account";
            public const string CertificateIdOrNameAttribute = "certificate";
            public const string UpdateExistingAttribute = "updateIfExisting";

            public static class CreateAccount
            {
                public const string NameAttribute = "name";
                public const string AccountTypeAttribute = "type";

                public static class CreateTokenAccount
                {
                    [ServiceMessageName] public const string Name = "create-tokenaccount";
                    public const string Token = "token";
                }

                public static class CreateUserPassAccount
                {
                    [ServiceMessageName] public const string Name = "create-userpassaccount";
                    public const string Username = "username";
                    public const string Password = "password";
                }

                public static class CreateAwsAccount
                {
                    [ServiceMessageName] public const string Name = "create-awsaccount";
                    public const string SecretKey = "secretKey";
                    public const string AccessKey = "accessKey";
                }

                public static class CreateAzureAccount
                {
                    [ServiceMessageName] public const string Name = "create-azureaccount";
                    public const string SubscriptionAttribute = "azSubscriptionId";

                    public static class ServicePrincipal
                    {
                        public const string TypeName = "serviceprincipal";
                        public const string ApplicationAttribute = "azApplicationId";
                        public const string TenantAttribute = "azTenantId";
                        public const string PasswordAttribute = "azPassword";
                        public const string EnvironmentAttribute = "azEnvironment";
                        public const string BaseUriAttribute = "azBaseUri";
                        public const string ResourceManagementBaseUriAttribute = "azResourceManagementBaseUri";
                    }
                }
            }

            public static class CreateTarget
            {
                public const string NameAttribute = "name";
                public const string RolesAttribute = "roles";

                public static class CreateKubernetesTarget
                {
                    [ServiceMessageName] public const string Name = "create-kubernetestarget";
                    public const string Namespace = "namespace";
                    public const string ClusterUrl = "clusterUrl";
                    public const string DefaultWorkerPool = "defaultWorkerPool";
                    public const string SkipTlsVerification = "skipTlsVerification";
                    public const string ClusterName = "clusterName";
                    public const string ClusterResourceGroup = "clusterResourceGroup";
                    public const string ClientCertificateIdOrName = "clientCertificate";
                    public const string ServerCertificateIdOrName = "serverCertificate";
                }

                public static class CreateAzureWebAppTarget
                {
                    [ServiceMessageName] public const string Name = "create-azurewebapptarget";
                    public const string WebAppNameAttribute = "webAppName";
                    public const string ResourceGroupNameAttribute = "resourceGroupName";
                    public const string WebAppSlotNameAttribute = "webAppSlot";
                }

                public static class CreateAzureCloudServiceTarget
                {
                    [ServiceMessageName] public const string Name = "create-azurecloudservicetarget";
                    public const string AzureCloudServiceNameAttribute = "azureCloudServiceName";
                    public const string AzureStorageAccountAttribute = "azureStorageAccount";
                    public const string AzureDeploymentSlotAttribute = "azureDeploymentSlot";
                    public const string SwapAttribute = "swap";
                    public const string InstanceCountAttribute = "instanceCount";
                }

                public static class CreateAzureServiceFabricTarget
                {
                    [ServiceMessageName] public const string Name = "create-azureservicefabrictarget";
                    public const string ConnectionEndpointAttribute = "connectionEndpoint";
                    public const string SecurityModeAttribute = "securityMode";
                    public const string CertificateThumbprintAttribute = "certificateThumbprint";
                    public const string ActiveDirectoryUsernameAttribute = "activeDirectoryUsername";
                    public const string ActiveDirectoryPasswordAttribute = "activeDirectoryPassword";
                    public const string CertificateStoreLocationAttribute = "certificateStoreLocation";
                    public const string CertificateStoreNameAttribute = "certificateStoreName";
                }
            }

            public static class DeleteTarget
            {
                [ServiceMessageName] public const string Name = "delete-target";
                public const string MachineIdOrNameAttribute = "machine";
            }
        }
    }
}