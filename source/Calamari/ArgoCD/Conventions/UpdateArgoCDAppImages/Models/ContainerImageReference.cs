#if NET
#nullable enable
using System;

namespace Calamari.ArgoCD.Conventions.UpdateArgoCDAppImages.Models
{
    public class ContainerImageReference
    {
        public const string DefaultContainerRegistry = "docker.io";
        ContainerImageReference(string registry, string imageName, string tag, string customRegistry)
        {
            // Trim special case of "index.docker.io" to "docker.io" - simplifes check furtther down, and we never want to write out the full "index." version anyway.
            if (registry.Equals($"index.{DefaultContainerRegistry}", StringComparison.OrdinalIgnoreCase))
            {
                registry = DefaultContainerRegistry;
            }

            ImageName = imageName;
            Tag = tag;
            Registry = registry;

            CustomRegistry = string.IsNullOrEmpty(customRegistry) ? null : customRegistry;
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

            return new ContainerImageReference(registry.ToLowerInvariant(), imageName.ToLowerInvariant(), tag.ToLowerInvariant(), defaultRegistry.ToLowerInvariant());
        }

        public string Registry { get; }
        public string ImageName { get; }
        public string Tag { get; }

        internal string? CustomRegistry { get; }

        public bool IsMatch(ContainerImageReference other)
        {
            if (Equals(other))
            {
                return true;
            }

            return ImageName.Equals(other.ImageName, StringComparison.OrdinalIgnoreCase) && RegistriesMatch(this, other);
        }

        public bool IsTagChange(ContainerImageReference other)
        {
            if (IsMatch(other))
            {
                return !Tag.Equals(other.Tag, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        public string WithTag(string tag)
        {
            return $"{AsQualifiedString()}:{tag}";
        }

        string AsQualifiedString()
        {
            return string.IsNullOrEmpty(Registry) ? ImageName : $"{Registry}/{ImageName}";
        }

        static bool RegistriesMatch(ContainerImageReference reference1, ContainerImageReference reference2)
        {
            return string.Equals(NormalizeRegistry(reference1), NormalizeRegistry(reference2), StringComparison.OrdinalIgnoreCase);

            string NormalizeRegistry(ContainerImageReference imageReference)
            {
                if (!string.IsNullOrWhiteSpace(imageReference.Registry))
                {
                    return imageReference.Registry;
                }

                return string.IsNullOrEmpty(imageReference.CustomRegistry) ? DefaultContainerRegistry : imageReference.CustomRegistry;
            }
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Tag) ? AsQualifiedString() : $"{AsQualifiedString()}:{Tag}";
        }
    }
}
#endif