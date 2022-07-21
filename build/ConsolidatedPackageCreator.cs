using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Calamari.Build
{
    static class ConsolidatedPackageCreator
    {
        public static void Create(IEnumerable<SourceFile> sourceFiles, string destination)
        {
            using (var zip = ZipFile.Open(destination, ZipArchiveMode.Create))
            {
                WriteUniqueFilesToZip(sourceFiles, zip);

                var indexEntry = zip.CreateEntry("index.json", CompressionLevel.Fastest);
                using (var destStream = indexEntry.Open())
                    WriteIndexTo(destStream, sourceFiles);
            }
        } 

        private static void WriteUniqueFilesToZip(IEnumerable<SourceFile> sourceFiles, ZipArchive zip)
        {
            var uniqueFiles = sourceFiles
                .GroupBy(sourceFile => new {sourceFile.FullNameInDestinationArchive, sourceFile.Hash})
                .Select(g => new
                {
                    g.Key.FullNameInDestinationArchive,
                    g.Key.Hash,
                    g.First().FullNameInSourceArchive,
                    g.First().ArchivePath
                });

            foreach (var groupedBySourceArchive in uniqueFiles.GroupBy(f => f.ArchivePath))
            {
                using (var sourceZip = ZipFile.OpenRead(groupedBySourceArchive.Key))
                    foreach (var uniqueFile in groupedBySourceArchive)
                    {
                        var entryName = Path.Combine(uniqueFile.Hash, uniqueFile.FullNameInDestinationArchive);
                        var entry = zip.CreateEntry(entryName, CompressionLevel.Fastest);

                        using (var destStream = entry.Open())
                        using (var sourceStream = sourceZip.Entries.First(e => e.FullName == uniqueFile.FullNameInSourceArchive).Open())
                            sourceStream.CopyTo(destStream);
                    }
            }
        }

        private static void WriteIndexTo(Stream stream, IEnumerable<SourceFile> sourceFiles)
        {
            Dictionary<string, string[]> GroupByPlatform(IEnumerable<SourceFile> filesForPackage)
                => filesForPackage
                    .GroupBy(f => f.Platform)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(f => f.Hash).OrderBy(h => h).ToArray()
                    );
            
            var index = new ConsolidatedPackageIndex(
                sourceFiles
                    .GroupBy(i => new {i.PackageId, i.Version, i.IsNupkg })
                    .ToDictionary(
                        g => g.Key.PackageId,
                        g => new ConsolidatedPackageIndex.Package(
                            g.Key.PackageId,
                            g.Key.Version,
                            g.Key.IsNupkg,
                            GroupByPlatform(g)
                        )
                    )
            );

            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(index, Formatting.Indented));
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}