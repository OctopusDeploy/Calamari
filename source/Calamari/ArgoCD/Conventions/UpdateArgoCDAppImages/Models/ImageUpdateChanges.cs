using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public class ImageUpdateChanges
    {
        public ImageUpdateChanges(HashSet<string> updatedFiles, HashSet<string> updatedImageReferences)
        {
            UpdatedFiles = updatedFiles;
            UpdatedImageReferences = updatedImageReferences;
        }

        public HashSet<string> UpdatedFiles { get; }
        public HashSet<string> UpdatedImageReferences { get; }
    }
}