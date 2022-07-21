using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Serilog;

namespace Calamari.Build
{
    class SashimiPackageReference : IPackageReference
    {
        private readonly Hasher hasher;
        private readonly BuildPackageReference packageReference;

        public SashimiPackageReference(Hasher hasher, BuildPackageReference packageReference)
        {
            this.hasher = hasher;
            this.packageReference = packageReference;
        }

        public string Name => packageReference.Name;
        public string Version => packageReference.Version;
        public string PackagePath => packageReference.PackagePath;

        public IReadOnlyList<SourceFile> GetSourceFiles(ILogger log)
        {
            // var toolZipsDir = Path.Combine(PackagePath, "tools");
            //
            // using (var zip = ZipFile.OpenRead(PackagePath))
            // {
            //     if (!zip.Entries.Any(e => e.FullName.StartsWith("tools")))
            //     {
            //         log.Information($"Skipping {Name} as it does not have any zip files in the tools folder: {toolZipsDir}");
            //         return Array.Empty<SourceFile>();
            //     }
            //
            //     var sourceFiles = new List<SourceFile>();
            //     var toolZips = zip.Entries.Where(e => e.FullName.StartsWith("tools/"));
            //     foreach (var toolZip in toolZips)
            //     {
            //         var toolZipPath = Path.Combine(Path.GetDirectoryName(PackagePath), toolZip.Name);
            //         toolZip.ExtractToFile(toolZipPath);
            //         
            //         sourceFiles.AddRange(ReadSashimiPackagedZip(toolZipPath));
            //     }
            //
            //     return sourceFiles;
            // }
            var extractPath = $"{Path.GetDirectoryName(PackagePath)}/extract/{Name}";
            ZipFile.ExtractToDirectory(PackagePath, extractPath);
            
            var toolZipsDir = Path.Combine(extractPath, "tools");

            if (!Directory.Exists(toolZipsDir))
            {
                log.Information($"Skipping {Name} as it does not have a tools folder: {toolZipsDir}");
                return Array.Empty<SourceFile>();
            }

            var toolZips = Directory.GetFiles(toolZipsDir);

            if (toolZips.Length == 0)
            {
                log.Information($"Skipping {Name} as it does not have any zip files in the tools folder: {toolZipsDir}");
                return Array.Empty<SourceFile>();
            }

            return toolZips.SelectMany(toolZipPath => ReadSashimiPackagedZip(toolZipPath))
                           .ToArray();
        }

        private IReadOnlyList<SourceFile> ReadSashimiPackagedZip(string toolZipPath)
        {
            using (var zip = ZipFile.OpenRead(toolZipPath))
                return zip.Entries
                          .Where(e => !string.IsNullOrEmpty(e.Name))
                          .Select(entry =>
                                  {
                                      // Sashimi zips have each full Calamari executable in folders according to platform
                                      var parts = entry.FullName.Split('/');
                                      return new SourceFile
                                      {
                                          PackageId = Path.GetFileNameWithoutExtension(toolZipPath),
                                          Version = Version,
                                          Platform = parts[0],
                                          ArchivePath = toolZipPath,
                                          IsNupkg = false,
                                          FullNameInDestinationArchive = string.Join("/", parts.Skip(1)),
                                          FullNameInSourceArchive = entry.FullName,
                                          Hash = hasher.Hash(entry)
                                      };
                                  })
                          .ToArray();
        }
    }
}