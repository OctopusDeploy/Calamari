namespace Calamari.Kubernetes.Authentication
{
    public static class AccountTypes
    {
        public const string UsernamePassword = nameof(UsernamePassword);
        public const string Token = nameof(Token);
        public const string AzureServicePrincipal = nameof(AzureServicePrincipal);
        public const string AzureOidc = nameof(AzureOidc);
        public const string GoogleCloudAccount = nameof(GoogleCloudAccount);
        public const string AmazonWebServicesAccount = nameof(AmazonWebServicesAccount);
        public const string AmazonWebServicesOidcAccount = nameof(AmazonWebServicesOidcAccount);    
    }
}