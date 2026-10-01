using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Execution;
using NUnit.Framework;
using Octopus.Calamari.ConsolidatedPackage;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Calamari.ConsolidateCalamariPackages.Tests;

[TestFixture]
public class ConsolidatedPackageTests
{
    [Test]
    public void ExtractCalamariPackage_ExtractsPlatformFiles()
    {
        // Taken from real Calamari index.json
        var index = new ConsolidatedPackageIndex(new Dictionary<string, IConsolidatedPackageIndex.Package>
        {
            ["Calamari"] =
                new("Calamari",
                    "2025.4.682",
                    IsNupkg: true,
                    new Dictionary<string, IConsolidatedPackageIndex.FileTransfer[]>
                    {
                        ["linux-x64"] =
                        [
                            new("e53319a5e0ad28139f18abff2a3846a2/Octopus.Calamari.linux-x64.nuspec", "Octopus.Calamari.linux-x64.nuspec"),
                            new("3103c6689c0c54d1951cd48c91fd07d3/Calamari.Shared.dll", "Calamari.Shared.dll"),
                            new("9530f39bf5be0d28a050a0d536354840/Calamari.dll", "Calamari.dll"),
                        ],
                        ["win-x64"] =
                        [
                            new("6f7d961cb2643f9b4676db6c6b2fb050/Octopus.Calamari.win-x64.nuspec", "Octopus.Calamari.win-x64.nuspec"),
                            new("aa773c55c461d2bc95d3ec23d0c2affc/Calamari.Shared.dll", "Calamari.Shared.dll"),
                            new("31e6370cd8472603f2082ada35be3cc3/Calamari.dll", "Calamari.dll"),
                        ],
                    })
        });

        var streamProvider =
            BuildZipArchive(("e53319a5e0ad28139f18abff2a3846a2/Octopus.Calamari.linux-x64.nuspec", "aaa_Octopus.Calamari.linux-x64"),
                            ("3103c6689c0c54d1951cd48c91fd07d3/Calamari.Shared.dll", "aab_Calamari.Shared.dll"),
                            ("9530f39bf5be0d28a050a0d536354840/Calamari.dll", "aac_Calamari.dll"),
                            ("6f7d961cb2643f9b4676db6c6b2fb050/Octopus.Calamari.win-x64.nuspec", "baa_Octopus.Calamari.linux-x64"),
                            ("aa773c55c461d2bc95d3ec23d0c2affc/Calamari.Shared.dll", "bab_Calamari.Shared.dll"),
                            ("31e6370cd8472603f2082ada35be3cc3/Calamari.dll", "bac_Calamari.dll"));

        var p = new ConsolidatedPackage(streamProvider, index);

        var linux64 = p.ExtractCalamariPackage("Calamari", "linux-x64").Select(i => i.entryName).ToArray();
        linux64.Should().Equal("Octopus.Calamari.linux-x64.nuspec", "Calamari.Shared.dll", "Calamari.dll");

        var win64 = p.ExtractCalamariPackage("Calamari", "win-x64").Select(i => i.entryName).ToArray();
        win64.Should().Equal("Octopus.Calamari.win-x64.nuspec", "Calamari.Shared.dll", "Calamari.dll");
    }

    [Test]
    public void ExtractCalamariPackage_ExtractsSameFileContentsToDifferentOutputs()
    {
        var index = new ConsolidatedPackageIndex(new Dictionary<string, IConsolidatedPackageIndex.Package>
        {
            ["Chicken"] =
                new("Chicken",
                    "1",
                    IsNupkg: true,
                    new Dictionary<string, IConsolidatedPackageIndex.FileTransfer[]>
                    {
                        ["linux-x64"] =
                        [
                            new("e53319a5e0ad28139f18abff2a3846a2/Chicken.txt", "Chicken.txt"),
                            new("e53319a5e0ad28139f18abff2a3846a2/Chicken.txt", "Subfolder/Chicken.txt"),
                        ],
                    })
        });

        var customStreamProvider = BuildZipArchive(("e53319a5e0ad28139f18abff2a3846a2/Chicken.txt", "Crazy chickens jab, peck, and flap while quietly dozing foxes get very humid"));

        var p = new ConsolidatedPackage(customStreamProvider, index);

        // ExtractCalamariPackage returns an IEnumerable containing streams which are closed as we move through the IEnumerable
        // We can't call ToArray on it or all the streams get closed before we can read them.
        using var enumerator = p.ExtractCalamariPackage("Chicken", "linux-x64").GetEnumerator();

        enumerator.MoveNext().Should().BeTrue();
        {
            var (entryName, size, sourceStream) = enumerator.Current;
            entryName.Should().Be("Chicken.txt");
            size.Should().Be(76);
            new StreamReader(sourceStream).ReadToEnd().Should().Be("Crazy chickens jab, peck, and flap while quietly dozing foxes get very humid");
        }

        enumerator.MoveNext().Should().BeTrue();
        {
            var (entryName, size, sourceStream) = enumerator.Current;
            entryName.Should().Be("Subfolder/Chicken.txt");
            size.Should().Be(76);
            new StreamReader(sourceStream).ReadToEnd().Should().Be("Crazy chickens jab, peck, and flap while quietly dozing foxes get very humid");
        }
        
        enumerator.MoveNext().Should().BeFalse(because: "No more files in the index");
    }

    class FakeConsolidatedPackageStreamProvider(byte[] zipFileContents) : IConsolidatedPackageStreamProvider
    {
        public Stream OpenStream() => new MemoryStream(zipFileContents);
    }

    static FakeConsolidatedPackageStreamProvider BuildZipArchive(params (string, string)[] filesAndContents)
    {
        using var stream = new MemoryStream();
        using (var z = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            foreach (var (file, content) in filesAndContents)
            {
                var entry = z.CreateEntry(file, CompressionLevel.NoCompression);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(content);
            }
        }

        return new(stream.ToArray());
    }
}