namespace Calamari.Common.Plumbing.Variables
{
    public static class AuthenticationVariables
    {
        public static readonly string FeedType = "FeedType";
        public static readonly string Jwt = "Jwt";
        
        public static class Azure
        {
            public static readonly string TenantId = "TenantId";
            public static readonly string ClientId = "ClientId";
        }
    }
}