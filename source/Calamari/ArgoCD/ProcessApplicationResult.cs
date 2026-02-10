using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD
{
    public record FilePathContent(string FilePath, string Content);

    public record UpdatedSourceDetail(
        string CommitSha,
        int SourceIndex,
        List<FilePathContent> ReplacedFiles,
        List<FilePathContent> PatchedFiles);

    public class ProcessApplicationResult
    {
        public ProcessApplicationResult(string gatewayId, ApplicationName applicationName)
        {
            GatewayId = gatewayId;
            ApplicationName = applicationName;
        }

        public string GatewayId { get; }
        public ApplicationName ApplicationName { get; }
        public int TotalSourceCount { get; init; }
        public int MatchingSourceCount { get; init; }
        public List<UpdatedSourceDetail> UpdatedSourceDetails { get; init; } = [];
        public HashSet<string> UpdatedImages { get; init; } = [];
        public HashSet<string> GitReposUpdated { get; init; } = [];
        public int UpdatedSourceCount => UpdatedSourceDetails.Count;
        public bool Updated => UpdatedSourceDetails.Count != 0;
    }
}
