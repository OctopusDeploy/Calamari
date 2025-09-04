#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages
{

    public interface ICommitMessageGenerator
    {
        string GenerateForImageUpdates(GitCommitSummary summary, string? userDescription, HashSet<string> updatedImages);
        string GenerateDescription(HashSet<string> updatedImages, string? userDescription);
    }

    public class CommitMessageGenerator : ICommitMessageGenerator
    {
        public string GenerateForImageUpdates(GitCommitSummary summary, string? userDescription, HashSet<string> updatedImages)
        {
            var description = GenerateDescription(updatedImages, userDescription);
            return $"{summary.Value}\n\n{description}";
        }

        public string GenerateDescription(HashSet<string> updatedImages, string? userDescription)
        {
            var updatedImagesList = GenerateUpdatedImagesList(updatedImages);
            return string.IsNullOrEmpty(userDescription)
                ? updatedImagesList
                : $"{userDescription}\n\n{updatedImagesList}";
        }

        string GenerateUpdatedImagesList(HashSet<string> updatedImages)
        {
            if (updatedImages.Any())
            {
                return "---\nImages updated:\n"
                       + string.Join("\n",
                                     updatedImages.OrderBy(container => container, StringComparer.OrdinalIgnoreCase) // Sort alphabetically for consistent output
                                                  .Select(container => $"- {container}"));
            }

            return "---\nNo images updated";
        }
    }
}
