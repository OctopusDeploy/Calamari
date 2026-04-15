#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Git
{
    public interface ICommitMessageGenerator
    {
        string GenerateDescription(FileUpdateResult result);
    }

    public class ImageTagUpdateCommitMessageGenerator : ICommitMessageGenerator
    {
        readonly GitCommitParameters gitCommitParameters;

        public ImageTagUpdateCommitMessageGenerator(GitCommitParameters gitCommitParameters)
        {
            this.gitCommitParameters = gitCommitParameters;
        }
        public string GenerateDescription(FileUpdateResult result)
        {
            var updatedImagesList = GenerateUpdatedImagesListCommitBody(result.UpdatedImages);
            return string.IsNullOrEmpty(gitCommitParameters.Description)
                ? updatedImagesList
                : $"{gitCommitParameters.Description}\n\n{updatedImagesList}";
        }
        
        public static string GenerateUpdatedImagesListCommitBody(HashSet<string> updatedImages)
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
    
    public class UserDefinedCommitMessageGenerator : ICommitMessageGenerator
    {
        readonly GitCommitParameters gitCommitParameters;

        public UserDefinedCommitMessageGenerator(GitCommitParameters gitCommitParameters)
        {
            this.gitCommitParameters = gitCommitParameters;
        }

        public string GenerateDescription(FileUpdateResult result)
        {
            return gitCommitParameters.Description;
        }
    }
}