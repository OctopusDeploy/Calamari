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
    }
}