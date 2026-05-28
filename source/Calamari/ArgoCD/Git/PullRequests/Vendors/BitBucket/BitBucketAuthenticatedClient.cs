using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.BitBucket
{
    public class BitBucketAuthenticatedClient : BitBucketGitClient, IGitVendorAuthenticatedClient
    {
        readonly IHttpsGitConnection repositoryConnection;

        public BitBucketAuthenticatedClient(IHttpsGitConnection repositoryConnection, Uri baseUrl)
            : base(repositoryConnection.Uri.Value, baseUrl)
        {
            this.repositoryConnection = repositoryConnection;
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
    }
}
