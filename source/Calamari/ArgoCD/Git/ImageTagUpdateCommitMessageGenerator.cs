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
        readonly string userDefinedCommitMessage;

        public ImageTagUpdateCommitMessageGenerator(string userDefinedCommitMessage)
        {
            this.userDefinedCommitMessage = userDefinedCommitMessage;
        }
        public string GenerateDescription(FileUpdateResult result)
        {
            var updatedImagesList = GenerateUpdatedImagesListCommitBody(result.UpdatedImages);
            return string.IsNullOrEmpty(userDefinedCommitMessage)
                ? updatedImagesList
                : $"{userDefinedCommitMessage}\n\n{updatedImagesList}";
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
        readonly string userDefinedCommitMessage;

        public UserDefinedCommitMessageGenerator(string userDefinedCommitMessage)
        {
            this.userDefinedCommitMessage = userDefinedCommitMessage;
        }

        public string GenerateDescription(FileUpdateResult result)
        {
            return userDefinedCommitMessage;
        }
    }
}