using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.ArgoCD.Git.GitVendorApiAdapters
{
    public class BitBucketApiAdapter : IGitVendorApiAdapter
    {
        readonly IRepositoryConnection repositoryConnection;
        readonly Uri baseUrl;

        readonly string workspace;
        readonly string repositorySlug;
        public BitBucketApiAdapter(IRepositoryConnection repositoryConnection, Uri baseUrl)
        {
            this.repositoryConnection = repositoryConnection;
            this.baseUrl = baseUrl;

            var parts = repositoryConnection.Url.ExtractPropertiesFromUrlPath();
            workspace = parts[0];
            repositorySlug = parts[1];
        }

        public async Task<PullRequest> CreatePullRequest(string pullRequestTitle,
                                                         string body,
                                                         GitBranchName sourceBranch,
                                                         GitBranchName destinationBranch,
                                                         CancellationToken cancellationToken)
        {
            // As Per https://support.atlassian.com/bitbucket-cloud/docs/using-api-tokens/ 
            // Bitbucket Cloud requires email address to be used as username for api, but username as username for Git

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic ",
                                                                                            Convert.ToBase64String(Encoding.ASCII.GetBytes($"robert.erez@gmail.com:{repositoryConnection.Password}")));
            
               // client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                //                                                                           Convert.ToBase64String(Encoding.ASCII.GetBytes($"{repositoryConnection.Password}")));
            var pullRequest = new
            {
                title = pullRequestTitle,
                source = new
                {
                    branch = new { name = sourceBranch.ToFriendlyName() },
                },
                destination = new
                {
                    branch = new { name = destinationBranch.ToFriendlyName() },
                },
                description = new
                {
                    markup = body
                }
            };

            var jsonContent = JsonConvert.SerializeObject(pullRequest);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            var apiUrl = $"https://api.bitbucket.org/2.0/repositories/{workspace}/{repositorySlug}/pullrequests";
            var response = await client.PostAsync(apiUrl, content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                var responseObject = JObject.Parse(responseBody);
                var pullRequestId = responseObject.Value<int>("pullRequestId");
                var prUrl = $"{baseUrl.AbsoluteUri}/{workspace}/{repositorySlug}/pull-requests/{pullRequestId}";
                return new PullRequest(responseObject["title"]!.ToString(), pullRequestId, prUrl);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception(errorContent);
        }

        public string GenerateCommitUrl(string commit)
        {
            return $"{baseUrl.AbsoluteUri}/{workspace}/{repositorySlug}/commits/{commit}";
        }
    }
}