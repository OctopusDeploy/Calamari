using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Assent;
using Calamari.ConsolidateCalamariPackages.Transferrable;
using FluentAssertions;
using NSubstitute;
using NuGet.Packaging;
using NUnit.Framework;
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
        readonly Assent.Configuration assentConfiguration = new Assent.Configuration().UsingSanitiser(s => Sanitise4PartVersions(SanitiseHashes(s)));
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
                this.Assent(string.Join("\r\n", zip.Entries.Select(e => SanitiseHashes(e.FullName)).OrderBy(k => k)), assentConfiguration);
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
            var consolidatedPackage = ConsolidatedPackage.Create(expectedZip);
            
            foreach (var keyValuePair in consolidatedPackage.Index.Packages)
            {
                var package = keyValuePair.Value;
                foreach (var platform in package.PlatformHashes.Keys)
                {
                    var outputFilename = $"{package.PackageId}.{platform}.zip";
                    var outputFilepath = Path.Combine(temp, outputFilename);
                    var calPackage = new CalamariPackage(new CalamariFlavour(keyValuePair.Key), $"{keyValuePair.Key}.{platform}", "noExe");
                    using (var outputStream = File.OpenWrite(outputFilepath))
                    using (var dest = new ZipWriter(outputStream, new ZipWriterOptions(SharpCompress.Common.CompressionType.Deflate) { DeflateCompressionLevel = CompressionLevel.BestSpeed }))
                    {
                        consolidatedPackage.PopulateArchive(calPackage, (name, size, source) => dest.Write(name, source));
                    }
                }
            }
            //
            // foreach (var reference in packageReferences)
            // {
            //     var packageId = IsNetfx(reference.Name) ? $"{reference.Name}.netfx" : reference.Name;
            //     var flavour = IsNetfx(reference.Name) ? reference.Name : reference.Name.Split(".")[0];
            //     var calPackage = new CalamariPackage(new CalamariFlavour(flavour), packageId, "noExe");
            //     var outputFilename = Path.Combine(temp, $"{packageId}_output.zip");
            //     using (var outputStream = File.OpenWrite(outputFilename))
            //     using (var dest = new ZipWriter(outputStream, new ZipWriterOptions(SharpCompress.Common.CompressionType.Deflate) { DeflateCompressionLevel = CompressionLevel.BestSpeed }))
            //     {
            //         consolidatedPackage.PopulateArchive(calPackage, (name, size, source) => dest.Write(name, source));
            //     }
            // }
        }

        public bool IsNetfx(string platformId)
        {
            return platformId.Equals("Calamari") || platformId.Equals("Calamari.Cloud");
        }

        private static string SanitiseHashes(string s)
            => Regex.Replace(s, "[a-z0-9]{32}", "<hash>");

        private static string Sanitise4PartVersions(string s)
            => Regex.Replace(s, @"[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+", "<version>");

        [Test]
        public void Execute()
            => this.BDDfy();
    }
}
