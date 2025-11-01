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
        readonly GitConnection repositoryConnection;

        string workspace;
        string repositorySlug;
        public BitBucketApiAdapter(GitConnection repositoryConnection)
        {
            this.repositoryConnection = repositoryConnection;
            workspace = "";
            repositorySlug = "";
        }

        public async Task<PullRequest> CreatePullRequest(string pullRequestTitle,
                                                         string body,
                                                         GitBranchName sourceBranch,
                                                         GitBranchName destinationBranch,
                                                         CancellationToken cancellationToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                                                                                       Convert.ToBase64String(Encoding.ASCII.GetBytes($"{repositoryConnection.Username}:{repositoryConnection.Password}")));
                
            /*
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
                                                                                           Convert.ToBase64String(Encoding.ASCII.GetBytes($"{repositoryConnection.Password}")));*/
            var pullRequest = new
            {
                title = pullRequestTitle,
                source = new
                {
                    branch = new { name = sourceBranch.Value },
                },
                destination = new
                {
                    branch = new { name = destinationBranch.Value },
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
                var prUrl = $"https://bitbucket.org/{workspace}/{repositorySlug}/pull-requests/{pullRequestId}";
                return new PullRequest(responseObject["title"]!.ToString(), pullRequestId, prUrl);
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception(errorContent);
        }

        public string GenerateCommitUrl(string commit)
        {
            return $"https://bitbucket.org/{workspace}/{repositorySlug}/commits/{commit}";
        }
        

        static bool CanInvokeWith(Uri uri)
        {
            return false;
            //return uri.Host.Equals("bitbucket.org");
        }
    }
}