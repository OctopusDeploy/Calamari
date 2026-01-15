using System;
using System.Collections.Generic;

namespace Calamari.ArgoCD.Models
{
    public class ImageUpdateChanges
    {
        public ImageUpdateChanges(HashSet<string> updatedFiles, HashSet<string> updatedImageReferences)
        {
            UpdatedFiles = updatedFiles;
            UpdatedImageReferences = updatedImageReferences;
        }

        public IReadOnlySet<string> UpdatedFiles { get; }
        public IReadOnlySet<string> UpdatedImageReferences { get; }
    }
}
