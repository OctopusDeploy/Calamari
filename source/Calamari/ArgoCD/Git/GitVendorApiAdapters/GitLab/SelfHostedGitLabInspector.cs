using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

// This is duplicated from the OctopusDeploy server repo
// source/Octopus.Core/Features/Git/PullRequests/Vendors/GitLab/SelfHostedGitLabInspector.cs
// Adjustments required:
// * Namespace
// * removed references to IOctopusHttpClientFactory - construct HtpClient directly
// * removed memory cache
// * removed semaphoreslim

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters.GitLab;

public class SelfHostedGitLabInspector()
{
    public static string GetSelfHostedBaseRepositoryUrl(Uri repositoryUri) => repositoryUri.GetLeftPart(UriPartial.Authority); //this elides the path & query

    public async Task<bool> IsSelfHostedGitLabInstance(Uri repositoryUri, CancellationToken cancellationToken)
    {
        var selfHostedUri = GetSelfHostedBaseRepositoryUrl(repositoryUri);
        var key = $"gitlab_{selfHostedUri}";

        using var httpClient = new HttpClient();

        //we can make an anonymous HTTP call to `/api/v4` and inspect the headers for `X-GitLab-Meta`
        var apiUrl = new UriBuilder(selfHostedUri)
        {
            Path = "/api/v4"
        };

        bool hasGitLabHeader;
        try
        {
            var response = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, apiUrl.Uri),
                                                      HttpCompletionOption.ResponseHeadersRead,
                                                      cancellationToken);

            //all API requests from GitLab have this header (afaik)
            hasGitLabHeader = response.Headers.Contains("x-gitlab-meta");
        }
        catch
        {
            //any exception we ignore
            hasGitLabHeader = false;
        }

        return hasGitLabHeader;
    }
}