// Some of this class was based on code from https://github.com/NuGet/NuGet.Client.
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;

namespace Calamari.Common.Features.Packages.NuGet
{
    public class NupkgExtractor : IPackageExtractor
    {
        const string ExcludeExtension = ".nupkg.sha512";

        static readonly string[] ExcludePaths =
        {
            "_rels/",
            "package/services/metadata/",
            @"_rels\",
            @"package\services\metadata\",
            "[Content_Types].xml"
        };

        readonly ILog log;

        public NupkgExtractor(ILog log)
        {
            this.log = log;
        }

        public string[] Extensions => new[] { ".nupkg" };

        public int Extract(string packageFile, string directory)
        {
            var filesExtracted = 0;

            using (var packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var archive = ZipArchive.Open(packageStream))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry == null) continue;
                    
                    var unescapedKey = UnescapePath(entry.Key);

                    if (unescapedKey == null || IsExcludedPath(unescapedKey))
                        continue;

                    var directoryName = Path.GetDirectoryName(unescapedKey);
                    if (directoryName == null)
                        continue;
                    
                    var targetDirectory = Path.Combine(directory, directoryName);

                    if (!Directory.Exists(targetDirectory))
                        Directory.CreateDirectory(targetDirectory);

                    if (entry.IsDirectory || !IsPackageFile(entry.Key))
                        continue;

                    var targetFile = Path.Combine(targetDirectory, Path.GetFileName(unescapedKey));
                    entry.WriteToFile(targetFile, new PackageExtractionOptions(log) { ExtractFullPath = false, PreserveFileTime = false });

                    SetFileLastModifiedTime(entry, targetFile);

                    filesExtracted++;
                }

                return filesExtracted;
            }
        }

        void SetFileLastModifiedTime(IEntry entry, string targetFile)
        {
            // Using the SharpCompress PreserveFileTime option caused an exception when unpacking
            // NuGet packages on Linux. So we set the LastModifiedTime ourselves.
            if (entry.LastModifiedTime.HasValue
                && entry.LastModifiedTime.Value != DateTime.MinValue
                && entry.LastModifiedTime.Value.ToUniversalTime() <= DateTime.UtcNow)
                try
                {
                    File.SetLastWriteTimeUtc(targetFile, entry.LastModifiedTime.Value.ToUniversalTime());
                }
                catch (Exception ex)
                {
                    log.Verbose($"Unable to set LastWriteTime attribute on file '{targetFile}':  {ex.Message}");
                }
        }

        static bool IsPackageFile(string? packageFileName)
        {
            if (string.IsNullOrEmpty(packageFileName) || string.IsNullOrEmpty(Path.GetFileName(packageFileName)))
                // This is to ignore archive entries that are not really files
                return false;

            if (IsManifest(packageFileName))
                return false;

            return !IsExcludedPath(packageFileName);
        }

        static bool IsExcludedPath(string path)
        {
            return ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                   || path.EndsWith(ExcludeExtension, StringComparison.OrdinalIgnoreCase);
        }

        static bool IsManifest(string path)
        {
            return Path.GetExtension(path).Equals(".nuspec", StringComparison.OrdinalIgnoreCase);
        }

        [return: NotNullIfNotNull("path")]
        static string? UnescapePath(string? path)
        {
            if (path != null && path.IndexOf('%') > -1)
                try
                {
                    return Uri.UnescapeDataString(path);
                }
                catch (UriFormatException)
                {
                    // on windows server 2003 we can get UriFormatExceptions when the original unescaped string contained %, just swallow and return path
                }

            return path;
        }
    }
}