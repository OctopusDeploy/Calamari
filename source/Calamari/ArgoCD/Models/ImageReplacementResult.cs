#nullable enable
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class ImageReplacementResult(string updatedContents, HashSet<string> updatedImageReferences, HashSet<string> alreadyUpToDateImages)
    {
        public string UpdatedContents { get; } = updatedContents;
        public HashSet<string> UpdatedImageReferences { get; } = updatedImageReferences;

        // Images whose name matched but whose tag was already at the target — no commit needed.
        // Note: an image can appear in both this set and UpdatedImageReferences if multiple containers
        // reference the same image name with different tags.
        public HashSet<string> AlreadyUpToDateImages { get; } = alreadyUpToDateImages;

        internal static ImageReplacementResult CombineResults(params ImageReplacementResult[] results)
        {
            if (results == null || results.Length == 0)
                return new ImageReplacementResult(string.Empty, new HashSet<string>(), new HashSet<string>());

            var allReplacements = new HashSet<string>();
            var allAlreadyUpToDate = new HashSet<string>();
            var latestContent = string.Empty;

            foreach (var result in results)
            {
                foreach (var replacement in result.UpdatedImageReferences)
                    allReplacements.Add(replacement);

                foreach (var upToDate in result.AlreadyUpToDateImages)
                    allAlreadyUpToDate.Add(upToDate);

                if (!string.IsNullOrEmpty(result.UpdatedContents))
                    latestContent = result.UpdatedContents;
            }

            return new ImageReplacementResult(latestContent, allReplacements, allAlreadyUpToDate);
        }
    }
}