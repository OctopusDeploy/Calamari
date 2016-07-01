// Some of this class was based on code from https://github.com/NuGet/NuGet.Client.
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using NuGet;
using SharpCompress.Common;
using SharpCompress.Reader;
using SharpCompress.Reader.Zip;

namespace Calamari.Integration.Packages.NuGet
{
    public class NupkgExtractor : IPackageExtractor
    {
        public string[] Extensions => new[] {global::NuGet.Constants.PackageExtension};

        public PackageMetadata GetMetadata(string packageFile)
        {
            using (var packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var reader = ZipReader.Open(packageStream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (reader.Entry.IsDirectory || !IsManifest(reader.Entry.Key))
                        continue;

                    using (var manifestStream = reader.OpenEntryStream())
                    {
                        var manifest = Manifest.ReadFrom(manifestStream, validateSchema: false);
                        var packageMetadata = (IPackageMetadata)manifest.Metadata;
                        return new PackageMetadata
                        {
                            Id = packageMetadata.Id,
                            Version = packageMetadata.Version.ToString(),
                            FileExtension = Extensions.First()
                        };
                    }
                }

                throw new InvalidOperationException("Package does not contain a manifest");
            }
        }

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var filesExtracted = 0; 

            using (var packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var reader = ZipReader.Open(packageStream))
            {
                while (reader.MoveToNextEntry())
                {
                    var targetDirectory = Path.Combine(directory, UnescapePath(Path.GetDirectoryName(reader.Entry.Key)));

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    if (reader.Entry.IsDirectory || !IsPackageFile(reader.Entry.Key))
                        continue;

                    var targetFile = UnescapePath(Path.GetFileName(reader.Entry.Key));
                    reader.WriteEntryToFile(Path.Combine(targetDirectory, targetFile), ExtractOptions.Overwrite | ExtractOptions.PreserveFileTime);
                    filesExtracted++;

                    if (!suppressNestedScriptWarning)
                        GenericPackageExtractor.WarnIfScriptInSubFolder(reader.Entry.Key);
                }

                return filesExtracted;
            }
        }

        static bool IsPackageFile(string packageFileName)
        {
            if (string.IsNullOrEmpty(packageFileName)
                || string.IsNullOrEmpty(Path.GetFileName(packageFileName)))
            {
                // This is to ignore archive entries that are not really files
                return false;
            }

            if (IsManifest(packageFileName))
                return false;

            return !ExcludePaths.Any(p => packageFileName.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                !packageFileName.EndsWith(ExcludeExtension, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(global::NuGet.Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static string UnescapePath(string path)
        {
            if (path != null
                && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        static readonly string[] ExcludePaths = new[]
        {
            "_rels/",
            "package/",
            @"_rels\",
            @"package\",
            "[Content_Types].xml"
        };

        const string ExcludeExtension = ".nupkg.sha512";
    }
}