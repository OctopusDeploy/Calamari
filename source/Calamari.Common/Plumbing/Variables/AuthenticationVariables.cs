namespace Calamari.Common.Plumbing.Variables
{
    public static class AuthenticationVariables
    {
        public static readonly string FeedType = "FeedType";
        public static readonly string Jwt = "Jwt";
        
        public static class Aws
        {
            public static readonly string RoleArn = "RoleArn";
            public static readonly string Region = "Region";
            public static readonly string SessionDuration = "SessionDuration";
        }
        
        public static class Azure
        {
            public static readonly string TenantId = "TenantId";
            public static readonly string ClientId = "ClientId";
        }
        
        public static class Google
        {
            public static readonly string Audience = "Audience";
        }
    }
}