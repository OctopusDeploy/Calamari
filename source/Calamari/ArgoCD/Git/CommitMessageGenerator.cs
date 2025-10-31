#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Git
{
    public interface ICommitMessageGenerator
    {
        string GenerateDescription(HashSet<string> updatedImages, string? userDescription);
    }

    public class CommitMessageGenerator : ICommitMessageGenerator
    {
        // TODO: This is a leaky abstraction - figure out how to remove
        public string GenerateDescription(HashSet<string> updatedImages, string? userDescription)
        {
            var updatedImagesList = GenerateUpdatedImagesListCommitBody(updatedImages);
            return string.IsNullOrEmpty(userDescription)
                ? updatedImagesList
                : $"{userDescription}\n\n{updatedImagesList}";
        }

        string GenerateUpdatedImagesListCommitBody(HashSet<string> updatedImages)
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