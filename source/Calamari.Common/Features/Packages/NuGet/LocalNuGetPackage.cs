#if USE_NUGET_V3_LIBS
using NuGet.Packaging;
#else
using NuGet;
#endif
using System;
using System.IO;
using Calamari.Common.Plumbing;
using SharpCompress.Archives.Zip;

namespace Calamari.Common.Features.Packages.NuGet
{
    public class LocalNuGetPackage
    {
        readonly string filePath;
        readonly Lazy<ManifestMetadata> metadata;

        public LocalNuGetPackage(string filePath)
        {
            Guard.NotNullOrWhiteSpace(filePath, "Must supply a non-empty file-path");

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Could not find package file '{filePath}'");

            this.filePath = filePath;
            metadata = new Lazy<ManifestMetadata>(() => ReadMetadata(this.filePath));
        }

        public ManifestMetadata Metadata => metadata.Value;

        public void GetStream(Action<Stream> process)
        {
            using (var fileStream = File.OpenRead(filePath))
            {
                process(fileStream);
            }
        }

        static ManifestMetadata ReadMetadata(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (var archive = ZipArchive.Open(fileStream))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.IsDirectory || !IsManifest(entry.Key))
                        continue;

                    using (var manifestStream = entry.OpenEntryStream())
                    {
                        // NuGet keeps adding new elements to the NuSpec schema,
                        // which in turn breaks us when we try to read the manifest,
                        // so we now read the manifest without schema validation
                        // https://github.com/OctopusDeploy/Issues/issues/3487
                        var manifest = Manifest.ReadFrom(manifestStream, false);
                        return manifest.Metadata;
                    }
                }

                throw new InvalidOperationException("Package does not contain a manifest");
            }
        }

        static bool IsManifest(string? path)
        {
            return path != null && Path.GetExtension(path).Equals(".nuspec", StringComparison.OrdinalIgnoreCase);
        }
    }
}