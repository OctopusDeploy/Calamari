// ReSharper disable RedundantUsingDirective

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Nuke.Common.CI.TeamCity;
using Nuke.Common.IO;
using Serilog;

namespace Calamari.Build.Utilities
{
    class Ci
    {
        public static void ZipFolderAndUploadArtifact(AbsolutePath folderPath, AbsolutePath outputPath)
        {
            if (!Directory.EnumerateFiles(folderPath).Any()
                && !Directory.EnumerateDirectories(folderPath).Any())
            {
                Log.Information("Could not find any file or folder in {FolderPath} to zip into {ZipFileName}", folderPath, outputPath);
                return;
            }

            var startTimestamp = Stopwatch.GetTimestamp();

            Log.Information("Zipping folder {FolderPath} into {ZipFileName} - {Timestamp}", folderPath, outputPath, DateTime.Now);
            ZipFile.CreateFromDirectory(folderPath, outputPath);

            var stopTimestamp = Stopwatch.GetTimestamp();

            Log.Information("Completed zipping folder {FolderPath} into {ZipFileName} in {Elapsed}", folderPath, outputPath, Stopwatch.GetElapsedTime(startTimestamp, stopTimestamp));

            TeamCity.Instance?.PublishArtifacts($"{outputPath}=>/");
        }
    }
}
