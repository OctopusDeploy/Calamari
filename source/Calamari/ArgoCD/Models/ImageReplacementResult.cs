using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class ImageReplacementResult
    {
        public ImageReplacementResult(string updatedContents, HashSet<string> updatedImageReferences, HashSet<string>? alreadyUpToDateImages = null)
        {
            UpdatedContents = updatedContents;
            UpdatedImageReferences = updatedImageReferences;
            AlreadyUpToDateImages = alreadyUpToDateImages ?? new HashSet<string>();
        }

        public string UpdatedContents { get; }
        public HashSet<string> UpdatedImageReferences { get; }
        // Images whose name matched but whose tag was already at the target — no commit needed.
        // Note: an image can appear in both this set and UpdatedImageReferences if multiple containers
        // reference the same image name with different tags.
        public HashSet<string> AlreadyUpToDateImages { get; }
    }
}