using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Octopus.Calamari.ConsolidatedPackage
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
            // This acts as a 'Distinct' - there may be multiples of a given file which are binary-identical
            // therefore we only need to track ONE (aka the first) of these files during packing.
            var uniqueFiles = sourceFiles
                .DistinctBy(sourceFile => sourceFile.EntryNameInConsolidationArchive());

            foreach (var groupedBySourceArchive in uniqueFiles.GroupBy(f => f.ArchivePath))
            {
                using (var sourceZip = ZipFile.OpenRead(groupedBySourceArchive.Key))
                    foreach (var uniqueFile in groupedBySourceArchive)
                    {
                        var entry = zip.CreateEntry(uniqueFile.EntryNameInConsolidationArchive(), CompressionLevel.Fastest);
                        using (var destStream = entry.Open())
                        using (var sourceStream = sourceZip.Entries.First(e => e.FullName == uniqueFile.FullNameInSourceArchive).Open())
                            sourceStream.CopyTo(destStream);
                    }
            }
        }
        
        private static void WriteIndexTo(Stream stream, IEnumerable<SourceFile> sourceFiles)
        {
            // SHould break out entryName to a function - make first class
            Dictionary<string, IConsolidatedPackageIndex.FileTransfer[]> GroupByPlatform(IEnumerable<SourceFile> filesForPackage)
                => filesForPackage
                   .GroupBy(f => f.Platform)
                   .ToDictionary(
                                 g => g.Key,
                                 g => g.Select(f => new IConsolidatedPackageIndex.FileTransfer(f.EntryNameInConsolidationArchive(), f.FullNameInDestinationArchive)).ToArray() 
                    );
            
            var index = new ConsolidatedPackageIndex(
                sourceFiles
                    .GroupBy(i => new {i.PackageId, i.Version, i.IsNupkg })
                    .ToDictionary(
                        g => g.Key.PackageId,
                        g => new IConsolidatedPackageIndex.Package(
                            g.Key.PackageId,
                            g.Key.Version,
                            g.Key.IsNupkg,
                            GroupByPlatform(g)
                        )
                    )
            );

            var bytes = Encoding.UTF8.GetBytes((string)JsonConvert.SerializeObject(index, Formatting.Indented));
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}