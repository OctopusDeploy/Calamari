#nullable enable
using System;

namespace Calamari.ArgoCD.Models
{
    public class ContainerImageReference
    {
        ContainerImageReference(string registry, string imageName, string tag, string defaultRegistry)
        {
            ImageName = imageName;
            Tag = tag;
            Registry = registry;

            //if you don't provide a default registry, we assume its the defined constant.
            DefaultRegistry = string.IsNullOrEmpty(defaultRegistry)
                ? ArgoCDConstants.DefaultContainerRegistry
                : defaultRegistry;
        }

        public static ContainerImageReference FromReferenceString(string containerImageReference, string defaultRegistry = "")
        {
            if (string.IsNullOrWhiteSpace(containerImageReference) || containerImageReference.Contains(' '))
            {
                throw new ArgumentNullException(nameof(containerImageReference), "Container image reference cannot be null, empty, or contain whitespace characters.");
            }

            // Additional checking handle if a registry includes a port.
            var lastColonIndex = containerImageReference.LastIndexOf(':');
            var lastSlashIndex = containerImageReference.LastIndexOf('/');

            string repoImagePart;
            string tag;

            // If there's a colon after the last slash, it separates the tag
            if (lastColonIndex != -1 && lastColonIndex > lastSlashIndex)
            {
                repoImagePart = containerImageReference[..lastColonIndex];
                tag = containerImageReference[(lastColonIndex + 1)..];
            }
            else
            {
                repoImagePart = containerImageReference;
                tag = string.Empty;
            }

            var repoParts = repoImagePart.Split('/');

            string registry;
            string imageName;

            if (repoParts.Length == 1)
            {
                // no registry, no namespace
                registry = string.Empty;
                imageName = repoParts[0];
            }
            else
            {
                if (repoParts[0].Contains('.') || repoParts[0].Contains(':') || repoParts[0] == "localhost")
                {
                    // first part is a registry
                    registry = repoParts[0];
                    imageName = string.Join('/', repoParts[1..]);
                }
                else
                {
                    // no registry, but possibly a namespace
                    registry = string.Empty;
                    imageName = string.Join('/', repoParts);
                }
            }

            //image tag is case-sensitive
            return new ContainerImageReference(registry.ToLowerInvariant(), imageName.ToLowerInvariant(), tag, defaultRegistry.ToLowerInvariant());
        }

        public string Registry { get; }
        public string ImageName { get; }
        public string Tag { get; }

        string DefaultRegistry { get; }

        public ContainerImageComparison CompareWith(ContainerImageReference other)
        {
            return new ContainerImageComparison(
                                                RegistriesMatch(this, other),
                                                ImageName.Equals(other.ImageName, StringComparison.OrdinalIgnoreCase),
                                                Tag.Equals(other.Tag)
                                               );
        }

        string ToOriginalFormatName()
        {
            //This will return the same format as originally supplied
            return string.IsNullOrEmpty(Registry) ? ImageName : $"{Registry}/{ImageName}";
        }

        public string WithTag(string tag)
        {
            return $"{ToOriginalFormatName()}:{tag}";
        }

        static bool RegistriesMatch(ContainerImageReference reference1, ContainerImageReference reference2)
        {
            return string.Equals(NormalizeRegistry(reference1), NormalizeRegistry(reference2), StringComparison.OrdinalIgnoreCase);

            string NormalizeRegistry(ContainerImageReference imageReference)
            {
                var registry =  !string.IsNullOrWhiteSpace(imageReference.Registry)
                    ? imageReference.Registry
                    : imageReference.DefaultRegistry;
                
                // Trim special case of "index.docker.io" to "docker.io" - simplifies check further down, and we never want to write out the full "index." version anyway.
                if (registry.Equals($"index.{ArgoCDConstants.DefaultContainerRegistry}", StringComparison.OrdinalIgnoreCase))
                {
                    return ArgoCDConstants.DefaultContainerRegistry;
                }

                return registry;
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Tag) ? ToOriginalFormatName() : $"{ToOriginalFormatName()}:{Tag}";
        }
    }

    public record ContainerImageComparison(bool RegistryMatch, bool ImageNameMatch, bool TagMatch)
    {
        public bool MatchesImageAndTag()
        {
            return RegistryMatch && ImageNameMatch && TagMatch;
        }

        public bool MatchesImage()
        {
            return RegistryMatch && ImageNameMatch;
        }
    }
}
