using System.Linq;

namespace Calamari.Kubernetes.Authentication
{
    public static class AccountTypes
    {
        public static readonly string[] KnownAccountTypes = new[]
        {
            UsernamePassword,
            Token,
            AzureServicePrincipal,
            AzureOidc,
            GoogleCloudAccount,
            AmazonWebServicesAccount
        };

        public const string UsernamePassword = nameof(UsernamePassword);
        public const string Token = nameof(Token);
        public const string AzureServicePrincipal = nameof(AzureServicePrincipal);
        public const string AzureOidc = nameof(AzureOidc);
        public const string GoogleCloudAccount = nameof(GoogleCloudAccount);
        public const string AmazonWebServicesAccount = nameof(AmazonWebServicesAccount);

        public static bool IsKnownAccountType(string accountType)
        {
            return KnownAccountTypes.Contains(accountType);
        }
    }
}