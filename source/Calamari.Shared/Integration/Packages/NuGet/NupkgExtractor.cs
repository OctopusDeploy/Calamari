﻿// Some of this class was based on code from https://github.com/NuGet/NuGet.Client.
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace Calamari.Integration.Packages.NuGet
{
    public class NupkgExtractor : IPackageExtractor
    {
        public string[] Extensions => new[] {".nupkg"};

        public int Extract(string packageFile, string directory, bool suppressNestedScriptWarning)
        {
            var filesExtracted = 0;

            using (var packageStream = new FileStream(packageFile, FileMode.Open, FileAccess.Read))
            using (var archive = ZipArchive.Open(packageStream))
            {
                foreach (var entry in archive.Entries)
                {
                    var unescapedKey = UnescapePath(entry.Key);

                    if (IsExcludedPath(unescapedKey))
                        continue;

                    var targetDirectory = Path.Combine(directory, Path.GetDirectoryName(unescapedKey));

                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    if (entry.IsDirectory || !IsPackageFile(entry.Key))
                        continue;

                    var targetFile = Path.Combine(targetDirectory, Path.GetFileName(unescapedKey));
                    entry.WriteToFile(targetFile, new ExtractionOptions { Overwrite = true, WriteSymbolicLink = WriteSymbolicLink });

                    SetFileLastModifiedTime(entry, targetFile);

                    filesExtracted++;

                    if (!suppressNestedScriptWarning)
                        GenericPackageExtractor.WarnIfScriptInSubFolder(unescapedKey);
                }

                return filesExtracted;
            }
        }

        static void WriteSymbolicLink(string sourcepath, string targetpath)
        {
            GenericPackageExtractor.WarnUnsupportedSymlinkExtraction(sourcepath);
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
            return Path.GetExtension(path).Equals(".nuspec", StringComparison.OrdinalIgnoreCase);
        }

        private static string UnescapePath(string path)
        {
            if (path != null && path.IndexOf('%') > -1)
            {
                try
                {
                    return Uri.UnescapeDataString(path);
                }
                catch (UriFormatException)
                {
                    // on windows server 2003 we can get UriFormatExceptions when the original unescaped string contained %, just swallow and return path
                }
            }

            return path;
        }

        static readonly string[] ExcludePaths =
        {
            "_rels/",
            "package/services/metadata/",
            @"_rels\",
            @"package\services\metadata\",
            "[Content_Types].xml"
        };

        const string ExcludeExtension = ".nupkg.sha512";
    }
}