using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Calamari.ConsolidateCalamariPackages.Tests.TestModels;
using FluentAssertions;
using NSubstitute;
using NuGet.Packaging;
using NUnit.Framework;
using Octopus.Calamari.ConsolidatedPackage;
using Serilog;
using TestStack.BDDfy;

namespace Calamari.ConsolidateCalamariPackages.Tests
{
    [TestFixture]
    public class IntegrationTests
    {
        const string ExpectedHash = "54d634ceb0b28d3d0463f4cd674461c5";
        
        IndexFile expectedIndexFile;
        
        const string TestPackagesDirectory = "./testPackages";
        string tempPath;
        string consolidatedPackageName;
        List<BuildPackageReference> packageReferences;
        bool returnValue;
        
        public void SetUp()
        {
            tempPath = Path.GetTempFileName(); 
            File.Delete(tempPath);
            Directory.CreateDirectory(tempPath);
            packageReferences = new List<BuildPackageReference>();
            consolidatedPackageName = Path.Combine(tempPath, $"Calamari.{ExpectedHash}.zip");

            var expectedIndexString = File.ReadAllText("./ExpectedIndex.json");
            expectedIndexFile = JsonSerializer.Deserialize<IndexFile>(expectedIndexString);
        }
        
        public void TearDown()
        {
            Directory.Delete(tempPath, true);
            Directory.Delete(TestPackagesDirectory, true);
        }
        
        public void GivenAFolderOfNugetPackages()
        {
            var packages = Directory.GetFiles(TestPackagesDirectory, "*.nupkg");
            
            // Unpack all Nuget packages in the temp directory, read their manifest, and add each file to the list of package references.
            foreach (var package in packages)
            {
                using (var zip = ZipFile.OpenRead(package))
                {
                    var nuspecFileStream = zip.Entries.First(e => e.Name.EndsWith(".nuspec")).Open();
                    var nuspecReader = new NuspecReader(nuspecFileStream);
                    var metadata = nuspecReader.GetMetadata().ToList();
                    packageReferences.Add(new BuildPackageReference
                    {
                        Name = metadata.Where(kvp => kvp.Key == "id").Select(i => i.Value).First(),
                        Version = metadata.Where(kvp => kvp.Key == "version").Select(i => i.Value).First(),
                        PackagePath = package
                    });
                }
            }
        }
        
        public void WhenConsolidated()
        {
            var sw = Stopwatch.StartNew();
            var task = new Consolidate(Substitute.For<ILogger>())
            {
                AssemblyVersion = "1.2.3"
            };
        
            (returnValue, _) = task.Execute(tempPath, packageReferences);
            Console.WriteLine($"Time: {sw.ElapsedMilliseconds:n0}ms");
        }
        
        public void ThenTheReturnValueIsTrue() => returnValue.Should().BeTrue();
        
        public void AndThenThePackageIsCreated()
        {
            Directory.GetFiles(tempPath).Should().ContainSingle(consolidatedPackageName);
            
            Console.WriteLine($"Package Size: {new FileInfo(consolidatedPackageName).Length / 1024 / 1024}MB");
        }
        
        public void AndThenThePackageContentsShouldMatch()
        {
            using (var zip = ZipFile.Open(consolidatedPackageName, ZipArchiveMode.Read))
            {
                var expectedFileList = File.ReadAllText("./ExpectedZipFileList.txt");
                var actualFileList = string.Join(Environment.NewLine, zip.Entries.Select(e => e.FullName.SanitiseHash().Sanitise4PartVersions()).OrderBy(s => s).ToList());
                
                actualFileList.Should().Be(expectedFileList);
            }
        }
        
        public void AndThenTheIndexShouldMatchExpectedIndex()
        {
            // NOTE: We migrated to this because different platforms change the order of files
            // and this approach removes any flakiness around that.
            
            // Read the index out of the package and ensure it matches the expected index
            using (var zip = ZipFile.Open(consolidatedPackageName, ZipArchiveMode.Read))
            using (var entry = zip.Entries.First(e => e.FullName == "index.json").Open())
            using (var sr = new StreamReader(entry))
            {
                var actualString = sr.ReadToEnd();
                var actualIndexFile  = JsonSerializer.Deserialize<IndexFile>(actualString);
                
                actualIndexFile.Should().BeEquivalentTo(expectedIndexFile);
            }
        }
        
        
        [Test]
        public void CheckFileConsolidation()
            => this.BDDfy();
    }
}
