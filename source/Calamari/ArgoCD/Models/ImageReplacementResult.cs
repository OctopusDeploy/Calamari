using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class ImageReplacementResult
    {
        public ImageReplacementResult(string updatedContents, HashSet<string> updatedImageReferences)
        {
            UpdatedContents = updatedContents;
            UpdatedImageReferences = updatedImageReferences;
        }

        public string UpdatedContents { get; }
        public HashSet<string> UpdatedImageReferences { get; }

        internal static ImageReplacementResult CombineResults(params ImageReplacementResult[] results)
        {
            if (results == null || results.Length == 0)
                return new ImageReplacementResult(string.Empty, new HashSet<string>());

            var allReplacements = new HashSet<string>();
            var latestContent = string.Empty;

            foreach (var result in results)
            {
                foreach (var replacement in result.UpdatedImageReferences)
                    allReplacements.Add(replacement);

                if (!string.IsNullOrEmpty(result.UpdatedContents))
                    latestContent = result.UpdatedContents;
            }

            return new ImageReplacementResult(latestContent, allReplacements);
        }
    }
}