using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SharpCompress.Common;
using SharpCompress.Writers;

namespace Calamari.Benchmarks.Support
{
    /// <summary>
    /// Writes package-shaped archives for the extraction benchmarks: a mix of nested directories and files,
    /// with incompressible payloads so inflate cost is not optimised away.
    /// </summary>
    public static class SyntheticArchive
    {
        public static readonly IReadOnlyDictionary<string, (ArchiveType Archive, CompressionType Compression)> Formats =
            new Dictionary<string, (ArchiveType, CompressionType)>(StringComparer.OrdinalIgnoreCase)
            {
                ["zip"] = (ArchiveType.Zip, CompressionType.Deflate),
                ["tar"] = (ArchiveType.Tar, CompressionType.None),
                ["tar.gz"] = (ArchiveType.Tar, CompressionType.GZip),
                ["tar.bz2"] = (ArchiveType.Tar, CompressionType.BZip2)
            };

        public static string Build(string directory, string format, int fileCount, int payloadBytes)
        {
            if (!Formats.TryGetValue(format, out var archiveFormat))
                throw new ArgumentOutOfRangeException(nameof(format), format, $"Unknown archive format. Known: {string.Join(", ", Formats.Keys)}");

            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"Benchmark.Package.1.0.0.{format}");
            if (File.Exists(path))
                File.Delete(path);

            using (var stream = File.OpenWrite(path))
            using (var writer = WriterFactory.Open(stream,
                                                   archiveFormat.Archive,
                                                   new WriterOptions(archiveFormat.Compression)
                                                   {
                                                       ArchiveEncoding = new ArchiveEncoding { Default = Encoding.UTF8 }
                                                   }))
            {
                for (var i = 0; i < fileCount; i++)
                {
                    // Spread files over a directory tree rather than a flat root, so the benchmark includes
                    // the directory-creation work that real package layouts force.
                    var entryName = $"content/group{i % 16:d2}/nested/file{i:d6}.bin";
                    writer.Write(entryName, new MemoryStream(Payload(entryName, payloadBytes)));
                }
            }

            return path;
        }

        static byte[] Payload(string seed, int payloadBytes)
        {
            var buffer = new byte[payloadBytes];
            new Random(seed.GetHashCode()).NextBytes(buffer);
            return buffer;
        }
    }
}
