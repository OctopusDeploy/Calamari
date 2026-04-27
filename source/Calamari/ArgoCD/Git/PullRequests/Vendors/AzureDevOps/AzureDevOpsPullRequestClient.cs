using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Calamari.ArgoCD.Git.PullRequests.Vendors.AzureDevOps
{
	public class AzureDevOpsPullRequestClient : IGitVendorPullRequestClient
	{
		const string CloudHost = "dev.azure.com";
		
		readonly HttpsGitConnection repositoryConnection;

		public AzureDevOpsPullRequestClient(HttpsGitConnection repositoryConnection)
		{
			this.repositoryConnection = repositoryConnection;
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

			
			var (organizationName, projectName, repositoryName) = AzureDevOpsRepositoryUriParser.Parse(repositoryConnection.Url.ParseAsHttpsUri());
			var apiUrl = $"https://{CloudHost}/{organizationName}/{projectName}/_apis/git/repositories/{repositoryName}/pullrequests?api-version=7.1";

			var pullRequest = new
			{
				sourceRefName = sourceBranch.Value,
				targetRefName = destinationBranch.Value,
				title = pullRequestTitle,
				description = body
			};

			var jsonContent = JsonConvert.SerializeObject(pullRequest);
			var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

			var response = await client.PostAsync(apiUrl, content, cancellationToken);

			if (response.IsSuccessStatusCode)
			{
				var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
				var responseObject = JObject.Parse(responseBody);
				var pullRequestId = responseObject["pullRequestId"]!.ToString();
				var prUrl = $"https://{CloudHost}/{organizationName}/{projectName}/_git/{repositoryName}/pullrequest/{pullRequestId}";
				return new PullRequest(responseObject["title"]!.ToString(), responseObject.Value<int>("pullRequestId"), prUrl);
			}

			var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
			throw new Exception(errorContent);
		}

		public string GenerateCommitUrl(string commit)
		{
			var (organizationName, projectName, repositoryName) = AzureDevOpsRepositoryUriParser.Parse(repositoryConnection.Url.ParseAsHttpsUri());
			return $"https://{CloudHost}/{organizationName}/{projectName}/_git/{repositoryName}/commit/{commit}";
		}
	}
}