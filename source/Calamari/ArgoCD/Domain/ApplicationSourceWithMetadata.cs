using System;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.ArgoCD.Domain
{
    public class ApplicationSourceWithMetadata
    {
        public ApplicationSourceWithMetadata(ApplicationSource source, SourceType? sourceType, int index)
        {
            Source = source;
            SourceType = sourceType;
            Index = index;
        }

        public ApplicationSource Source { get; }
        public SourceType? SourceType { get; }
        public int Index { get; }
        
        public string SourceIdentity => (Source.Name.IsNullOrEmpty() ? Source.RepoUrl.ToString() : Source.Name) + $" ({Index}:{SourceType.ToString() ?? "Unknown"})";
    }
}