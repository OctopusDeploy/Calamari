namespace Octopus.Calamari.Contracts.GitOps;

public static class ServiceMessages
{
    public static class PullRequestCreated
    {
        public const string Name = "pull-request-created";

        public static class Attributes
        {
            public const string PullRequestUri = "pullRequestUri";
            public const string RepositoryUri = "repositoryUri";
            public const string VendorName = "vendorName";
        }
    }

}