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
	public class AzureDevOpsApiAdapter : IGitVendorApiAdapter
	{
		readonly IRepositoryConnection repositoryConnection;

		readonly string organization;
		readonly string projectName;
		readonly string repositoryId;

		public AzureDevOpsApiAdapter(IRepositoryConnection repositoryConnection)
		{
			this.repositoryConnection = repositoryConnection;
			(organization, projectName, repositoryId) = ExtractUriComponents(repositoryConnection.Url);
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

			var apiUrl = $"https://{Host}/{organization}/{projectName}/_apis/git/repositories/{repositoryId}/pullrequests?api-version=7.1";

			var pullRequest = new
			{
				sourceRefName = sourceBranch.Value,
				targetRefName = destinationBranch.Value,
				title = pullRequestTitle,
				description = body
			};

			var jsonContent = JsonConvert.SerializeObject(pullRequest);
			var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

			var response = await client.PostAsync(apiUrl, content);

			if (response.IsSuccessStatusCode)
			{
				var responseBody = await response.Content.ReadAsStringAsync();
				var responseObject = JObject.Parse(responseBody);
				var pullRequestId = responseObject["pullRequestId"]!.ToString();
				var prUrl = $"https://{Host}/{organization}/{projectName}/_git/{repositoryId}/pullrequest/{pullRequestId}";
				return new PullRequest(responseObject["title"]!.ToString(), responseObject.Value<int>("pullRequestId"), prUrl);
			}
			else
			{
				var errorContent = await response.Content.ReadAsStringAsync();
				throw new Exception(errorContent);
				//Console.WriteLine($"Error creating Pull Request: {response.StatusCode} - {errorContent}");
			}
		}

		public string GenerateCommitUrl(string commit)
		{
			return $"https://{Host}/{organization}/{projectName}/_git/{repositoryId}/commit/{commit}";
		}

		static readonly string Host = "dev.azure.com";

		public static bool CanInvokeWith(Uri uri)
		{
			return uri.Host.Equals(Host);
		}

		public static (string organization, string projectName, string repositoryId) ExtractUriComponents(Uri uri)
		{
			// Example URI: https://robe-octopus@dev.azure.com/robe-octopus/octopus-testing/_git/secondaryrepo
			var parts = uri.ExtractPropertiesFromUrlPath();
			var organization = parts[0];
			var projectName = parts[1];
			if (parts[2] != "_git")
			{
				throw new InvalidOperationException($"Unexpected Uri Format: {uri.AbsoluteUri}");
			}

			var repositoryId = parts[3];
			return (organization, projectName, repositoryId);
		}
	}
}