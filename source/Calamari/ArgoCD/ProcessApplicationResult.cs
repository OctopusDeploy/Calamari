#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Models;

namespace Calamari.ArgoCD
{
    public record FileHash(string FilePath, string Hash);

    public record FileJsonPatch(string FilePath, string JsonPatch);

    public record TrackedSourceDetail(
        string? CommitSha,
        DateTimeOffset? CommitTimestamp,
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
            List<TrackedSourceDetail> trackedSourceDetails,
            HashSet<string> updatedImages,
            HashSet<string> gitReposUpdated)
        {
            GatewayId = gatewayId;
            ApplicationName = applicationName;
            TotalSourceCount = totalSourceCount;
            MatchingSourceCount = matchingSourceCount;
            TrackedSourceDetails = trackedSourceDetails;
            UpdatedImages = updatedImages;
            GitReposUpdated = gitReposUpdated;
        }

        public string GatewayId { get; }
        public ApplicationName ApplicationName { get; }
        public int TotalSourceCount { get; }
        public int MatchingSourceCount { get; }
        public List<TrackedSourceDetail> TrackedSourceDetails { get; }
        public HashSet<string> UpdatedImages { get; }
        public HashSet<string> GitReposUpdated { get; }
        public int UpdatedSourceCount => TrackedSourceDetails.Count(s => !string.IsNullOrEmpty(s.CommitSha));
        public bool Tracked => TrackedSourceDetails.Any();
        public bool Updated => UpdatedSourceCount > 0;
    }
}
