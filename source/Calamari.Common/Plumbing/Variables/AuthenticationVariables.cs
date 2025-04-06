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
    }
}