using System;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps;

// This is a copy of the code in Octopus Server. There are no tests here as there are tests in Server.
// https://github.com/OctopusDeploy/OctopusDeploy/blob/main/source/Octopus.Core/Features/Git/PullRequests/Vendors/AzureDevOps/AzureDevOpsRepositoryUriParser.cs
public static class AzureDevOpsRepositoryUriParser
{
    const string DevAzureHost = "dev.azure.com";
    const string VisualStudioHostSuffix = ".visualstudio.com";
    const string GitUrlPart = "_git";

    public static bool IsAzureDevOpsRepository(Uri uri)
        => uri.Host.EndsWith(VisualStudioHostSuffix, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals(DevAzureHost, StringComparison.OrdinalIgnoreCase);

    public static (string OrganizationName, string ProjectName, string RepositoryName) Parse(Uri repositoryUri)
    {
        if (!IsAzureDevOpsRepository(repositoryUri))
        {
            throw NotAzureDevOpsRepositoryException(repositoryUri);
        }

        string orgName;
        string projectName;
        string repoName;

        var parts = repositoryUri.SplitPathIntoSegments();

        // Example URI: https://organization-name.visualstudio.com/DefaultCollection/project-name/_git/repo-name
        // Example URI: https://organization-name.visualstudio.com/project-name/_git/repo-name
        if (repositoryUri.Host.EndsWith(VisualStudioHostSuffix, StringComparison.OrdinalIgnoreCase))
        {
            orgName = repositoryUri.Host[..^VisualStudioHostSuffix.Length];
            switch (parts.Length)
            {
                // Example Path: /DefaultCollection/project-name/_git/repo-name
                case 4:
                    if (parts[2] != GitUrlPart)
                    {
                        throw InvalidFormatException(repositoryUri);
                    }

                    projectName = parts[1];
                    repoName = parts[3];

                    break;

                // Example Path: /project-name/_git/repo-name
                case 3:
                    if (parts[1] != GitUrlPart)
                    {
                        throw InvalidFormatException(repositoryUri);
                    }

                    projectName = parts[0];
                    repoName = parts[2];

                    break;

                default:
                    throw InvalidFormatException(repositoryUri);
            }
        }
        // Example URI: https://organization-name@dev.azure.com/organization-name/project-name/_git/repo-name
        // Example URI: https://dev.azure.com/organization-name/project-name/_git/repo-name
        else if (repositoryUri.Host.Equals(DevAzureHost, StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length < 4)
            {
                throw InvalidFormatException(repositoryUri);
            }

            orgName = parts[0];
            projectName = parts[1];

            if (parts[2] != GitUrlPart)
            {
                throw InvalidFormatException(repositoryUri);
            }

            repoName = parts[3];
        }
        else
        {
            throw NotAzureDevOpsRepositoryException(repositoryUri);
        }

        return (orgName, projectName, repoName);
    }

    static InvalidOperationException NotAzureDevOpsRepositoryException(Uri repositoryUri) => new($"The repository URI does not point to a Azure DevOps repository. URI: {repositoryUri.AbsoluteUri}");
    static InvalidOperationException InvalidFormatException(Uri repositoryUri) => new($"The repository URI is in the incorrect format. URI: {repositoryUri.AbsoluteUri}");
}
