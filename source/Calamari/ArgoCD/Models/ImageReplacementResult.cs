#nullable enable
using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class ImageReplacementResult(string updatedContents, HashSet<string> updatedImageReferences, HashSet<string> alreadyUpToDateImages, HashSet<string>? unrecognisedKinds = null)
    {
        public string UpdatedContents { get; } = updatedContents;

        public HashSet<string> UpdatedImageReferences { get; } = updatedImageReferences;

        // Images whose name matched but whose tag was already at the target — no commit needed.
        // Note: an image can appear in both this set and UpdatedImageReferences if multiple containers
        // reference the same image name with different tags.
        public HashSet<string> AlreadyUpToDateImages { get; } = alreadyUpToDateImages;

        // Resource kinds (formatted as "apiVersion/kind") encountered in the YAML that this replacer
        // does not know how to process. Each type must be explicitly handled in ContainerImageReplacer.
        public HashSet<string> UnrecognisedKinds { get; } = unrecognisedKinds ?? [];
    }
}