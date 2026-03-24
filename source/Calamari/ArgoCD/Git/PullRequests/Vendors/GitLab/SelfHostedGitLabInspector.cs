using System.Net.Http;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using Octopus.Server.Extensibility.Extensions.Infrastructure.Web.Api;

namespace Octopus.Core.Features.Git.PullRequests.Vendors.GitLab;

public class SelfHostedGitLabInspector(IMemoryCache cache, IOctopusHttpClientFactory httpClientFactory)
{
    readonly IMemoryCache cache = cache;
    readonly IOctopusHttpClientFactory httpClientFactory = httpClientFactory;
    readonly SemaphoreSlim semaphoreSlim = new(1, 1);

    public static string GetSelfHostedBaseRepositoryUrl(Uri repositoryUri) => repositoryUri.GetLeftPart(UriPartial.Authority);//this elides the path & query

    public async Task<bool> IsSelfHostedGitLabInstance(Uri repositoryUri, CancellationToken cancellationToken)
    {
        var selfHostedUri = GetSelfHostedBaseRepositoryUrl(repositoryUri);
        var key = $"gitlab_{selfHostedUri}";    

        //we only want one piece of code checking this at once
        await semaphoreSlim.WaitAsync(cancellationToken);
        try
        {
            if (cache.TryGetValue<bool>(key, out var isSelfHosted))
            {
                return isSelfHosted;
            }

            var httpClient = httpClientFactory.CreateClient();

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

            //store this value and cache for 1 hr
            cache.Set(key, hasGitLabHeader, TimeSpan.FromHours(1));

            return hasGitLabHeader;
        }
        finally
        {
            semaphoreSlim.Release();
        }
    }
}
