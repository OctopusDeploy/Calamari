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
            var package = new LocalNuGetPackage(packageFile);
            var packageMetadata = package.Metadata;
            return new PackageMetadata
            {
                Id = packageMetadata.Id,
                Version = packageMetadata.Version,
                FileExtension = Extensions.First()
            };
        }

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var filesExtracted = 0; 

            using (var packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var reader = ZipReader.Open(packageStream))
            {
                while (reader.MoveToNextEntry())
                {
                    if (IsExcludedPath(reader.Entry.Key))
                        continue;

                    var targetDirectory = Path.Combine(directory, UnescapePath(Path.GetDirectoryName(reader.Entry.Key)));

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    if (reader.Entry.IsDirectory || !IsPackageFile(reader.Entry.Key))
                        continue;

                    var targetFile = Path.Combine(targetDirectory, UnescapePath(Path.GetFileName(reader.Entry.Key)));
                    reader.WriteEntryToFile(targetFile, ExtractOptions.Overwrite);

                    SetFileLastModifiedTime(reader.Entry, targetFile);

                    filesExtracted++;

                    if (!suppressNestedScriptWarning)
                        GenericPackageExtractor.WarnIfScriptInSubFolder(reader.Entry.Key);
                }

                return filesExtracted;
            }
        }

        static void SetFileLastModifiedTime(IEntry entry, string targetFile)
        {
            // Using the SharpCompress PreserveFileTime option caused an exception when unpacking
            // NuGet packages on Linux. So we set the LastModifiedTime ourselves.
            if (entry.LastModifiedTime.HasValue &&
                entry.LastModifiedTime.Value != DateTime.MinValue &&
                entry.LastModifiedTime.Value.ToUniversalTime() <= DateTime.UtcNow)
            {
                try
                {
                    File.SetLastWriteTimeUtc(targetFile, entry.LastModifiedTime.Value.ToUniversalTime());
                }
                catch (Exception ex)
                {
                    Log.Verbose($"Unable to set LastWriteTime attribute on file '{targetFile}':  {ex.Message}");
                }
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

            return !IsExcludedPath(packageFileName);
        }

        static bool IsExcludedPath(string path)
        {
            return ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || 
                path.EndsWith(ExcludeExtension, StringComparison.OrdinalIgnoreCase);
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