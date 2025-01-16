using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Assent;
using FluentAssertions;
using NSubstitute;
using NuGet.Packaging;
using NUnit.Framework;
using Octopus.Calamari.ConsolidatedPackage;
using Serilog;
using SharpCompress.Writers;
using SharpCompress.Writers.Zip;
using TestStack.BDDfy;
using CompressionLevel = SharpCompress.Compressors.Deflate.CompressionLevel;

namespace Calamari.ConsolidateCalamariPackages.Tests
{
    [TestFixture]
    public class IntegrationTests
    {
        readonly Assent.Configuration assentConfiguration = new Assent.Configuration().UsingSanitiser(s => Sanitise4PartVersions(SanitiseFilenamesInIndex(s)));
        static readonly string TestPackagesDirectory = "../../../testPackages";

        private string temp;
        private string expectedZip;
        private List<BuildPackageReference> packageReferences;
        private bool returnValue;

        public void SetUp()
        {
            temp = Path.GetTempFileName();
            File.Delete(temp);
            Directory.CreateDirectory(temp);
            packageReferences = new List<BuildPackageReference>();
            expectedZip = Path.Combine(temp, $"Calamari.3327050d788658cd16da010e75580d32.zip");
        }

        public void TearDown()
        {
            Directory.Delete(temp, true);
            Directory.Delete(TestPackagesDirectory, true);
        }

        public void GivenABunchOfPackageReferences()
        {
            var artifacts = Directory.GetFiles(TestPackagesDirectory, "*.nupkg");
            foreach (var artifact in artifacts)
            {
                using (var zip = ZipFile.OpenRead(artifact))
                {
                    var nuspecFileStream = zip.Entries.First(e => e.Name.EndsWith(".nuspec")).Open();
                    var nuspecReader = new NuspecReader(nuspecFileStream);
                    var metadata = nuspecReader.GetMetadata();
                    packageReferences.Add(new BuildPackageReference
                    {
                        Name = metadata.Where(kvp => kvp.Key == "id").Select(i => i.Value).First(),
                        Version = metadata.Where(kvp => kvp.Key == "version").Select(i => i.Value).First(),
                        PackagePath = artifact
                    });
                }
            }
        }

        public void WhenTheTaskIsExecuted()
        {
            var sw = Stopwatch.StartNew();
            var task = new Consolidate(Substitute.For<ILogger>())
            {
                AssemblyVersion = "1.2.3"
            };

            (returnValue, _) = task.Execute(temp, packageReferences);
            Console.WriteLine($"Time: {sw.ElapsedMilliseconds:n0}ms");
        }

        public void ThenTheReturnValueIsTrue()
            => returnValue.Should().BeTrue();

        public void AndThenThePackageIsCreated()
        {
            Directory.GetFiles(temp).Should().BeEquivalentTo(new[] {expectedZip});
            Console.WriteLine($"Package Size: {new FileInfo(expectedZip).Length / 1024 / 1024}MB");
        }

        public void AndThenThePackageContentsShouldBe()
        {
            using (var zip = ZipFile.Open(expectedZip, ZipArchiveMode.Read))
                this.Assent(string.Join("\r\n", zip.Entries.Select(e => SanitiseHashesInPackageList(e.FullName)).OrderBy(k => k)), assentConfiguration);
        }

        public void AndThenTheIndexShouldBe()
        {
            using (var zip = ZipFile.Open(expectedZip, ZipArchiveMode.Read))
            using (var entry = zip.Entries.First(e => e.FullName == "index.json").Open())
            using (var sr = new StreamReader(entry))
                this.Assent(sr.ReadToEnd(), assentConfiguration);
        }

        public void AndTheRegeneratedPackageShouldBeIdenticalToInputs()
        {
            var streamProvider = new FileBasedStreamProvider(expectedZip);
            var factory = new ConsolidatedPackageFactory();
            var consolidatedPackage = factory.LoadFrom(streamProvider);

            // Sashimi is a multi-arch package - atm this test can't unpack it cleanly enough.
            foreach (var reference in packageReferences.Where(pr => !pr.Name.Contains("Sashimi")))
            {
                var (flavour, platform) = ExtractFlavourAndPlatform(reference);
                var outputFilename = Path.Combine(temp, $"{flavour}_{platform}_output.zip");
                using (var outputStream = File.OpenWrite(outputFilename))
                {
                    using (var dest = new ZipWriter(outputStream, new ZipWriterOptions(SharpCompress.Common.CompressionType.Deflate) { DeflateCompressionLevel = CompressionLevel.BestSpeed, LeaveStreamOpen = false }))
                    {
                        foreach (var entry in consolidatedPackage.ExtractCalamariPackage(flavour, platform))
                        {
                            dest.Write(entry.destinationEntry, entry.sourceStream);
                        }
                    }
                }

                ZipFilesShouldBeIdentical(reference.PackagePath, outputFilename);
            }
        }

        void ZipFilesShouldBeIdentical(string inputFilename, string regeneratedZipFilename)
        {
            using (var inputZip = ZipFile.OpenRead(inputFilename))
            {
                var sourceEntries = inputZip.Entries.Where(e =>
                                                               !e.FullName.StartsWith("_rels") && !e.FullName.StartsWith("package") && !e.FullName.Equals("[Content_Types].xml")
                                                          )
                                            .ToList();
                using (var regenZip = ZipFile.OpenRead(regeneratedZipFilename))
                {
                    //NOTE: some files appear multiple times in the regenerated zip file
                    var regenNames = regenZip.Entries.Select(e => e.FullName).Distinct().ToList();
                    var sourceNames = sourceEntries.Select(e => e.FullName).ToList();
                    var missingNames = sourceNames.Where(s => !regenNames.Contains(s)).ToList();
                    var addedNames = regenNames.Where(s => !sourceNames.Contains(s)).ToList();
                    sourceNames.Should().BeEquivalentTo(regenNames);
                }
            }
        }

        static (string flavour, string platform) ExtractFlavourAndPlatform(BuildPackageReference packReference)
        {
            if (IsNetfx(packReference.Name))
            {
                return (packReference.Name, "netfx");
            }

            var packageName = packReference.Name;
            var flavour = packageName.Split(".")[0];
            var platform = packageName.Substring(flavour.Length).Trim('.');

            return (flavour, platform);
        }

        static bool IsNetfx(string packageId)
        {
            return packageId.Equals("Calamari") || packageId.Equals("Calamari.Cloud");
        }

        private static string SanitiseFilenamesInIndex(string s)
            => Regex.Replace(s, "[a-z0-9]{32}", "<hash>");
        
        private static string SanitiseHashesInPackageList(string s) 
        => Regex.Replace(s, "[a-z0-9]{32}", "<hash>");
        

        private static string Sanitise4PartVersions(string s)
            => Regex.Replace(s, @"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+", "<version>");

        [Test]
        public void Execute()
            => this.BDDfy();
    }
}
