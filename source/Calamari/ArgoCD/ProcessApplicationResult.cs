using System.Collections.Generic;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD
{
    public record FileHash(string FilePath, string Hash);

    public record FileJsonPatch(string FilePath, string JsonPatch);

    public record UpdatedSourceDetail(
        string CommitSha,
        int SourceIndex,
        List<FileHash> ReplacedFiles,
        List<FileJsonPatch> PatchedFiles);

    public class ProcessApplicationResult
    {
        public ProcessApplicationResult(
            string gatewayId,
            ApplicationName applicationName,
            int totalSourceCount,
            int matchingSourceCount,
            List<UpdatedSourceDetail> updatedSourceDetails,
            HashSet<string> updatedImages,
            HashSet<string> gitReposUpdated)
        {
            GatewayId = gatewayId;
            ApplicationName = applicationName;
            TotalSourceCount = totalSourceCount;
            MatchingSourceCount = matchingSourceCount;
            UpdatedSourceDetails = updatedSourceDetails;
            UpdatedImages = updatedImages;
            GitReposUpdated = gitReposUpdated;
        }

        public string GatewayId { get; }
        public ApplicationName ApplicationName { get; }
        public int TotalSourceCount { get; }
        public int MatchingSourceCount { get; }
        public List<UpdatedSourceDetail> UpdatedSourceDetails { get; }
        public HashSet<string> UpdatedImages { get; }
        public HashSet<string> GitReposUpdated { get; }
        public int UpdatedSourceCount => UpdatedSourceDetails.Count;
        public bool Updated => UpdatedSourceDetails.Count != 0;
    }
}
