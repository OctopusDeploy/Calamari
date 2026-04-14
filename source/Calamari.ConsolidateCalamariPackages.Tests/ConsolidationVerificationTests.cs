using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using NUnit.Framework;
using Octopus.Calamari.ConsolidatedPackage;
using Octopus.Calamari.ConsolidatedPackage.Api;

namespace Calamari.ConsolidateCalamariPackages.Tests
{
    record PackagePropertiesToTest(string[] Architectures, bool IsNupkg);

    [TestFixture]
    public class ConsolidationVerificationTests
    {
        string consolidatedFilePath = "";
        IConsolidatedPackageIndex consolidatedPackageIndex;
        string expectedVersion = "";

        const string WindowsX64Arch = "win-x64";

        static readonly string[] NetCoreArchitectures =
        [
            "linux-arm",
            "linux-arm64",
            "linux-x64",
            "osx-x64",
            WindowsX64Arch
        ];

        static Dictionary<string, PackagePropertiesToTest> PackagesWithDetails()
        {
            return new Dictionary<string, PackagePropertiesToTest>
            {
                { "Calamari", new PackagePropertiesToTest(NetCoreArchitectures, false /* this is no longer a nuget package */) },
                { "Calamari.AzureServiceFabric", new PackagePropertiesToTest([WindowsX64Arch], false) },
                { "Calamari.AzureAppService", new PackagePropertiesToTest(NetCoreArchitectures, false) },
                { "Calamari.AzureResourceGroup", new PackagePropertiesToTest(NetCoreArchitectures, false) },
                { "Calamari.GoogleCloudScripting", new PackagePropertiesToTest(NetCoreArchitectures, false) },
                { "Calamari.AzureScripting", new PackagePropertiesToTest(NetCoreArchitectures, false) },
                { "Calamari.AzureWebApp", new PackagePropertiesToTest([WindowsX64Arch], false) },
                { "Calamari.Terraform", new PackagePropertiesToTest(NetCoreArchitectures, false) }
            };
        }

        static readonly HashSet<string> PackagesWithDotnetScript =
        [
            "Calamari",
            "Calamari.AzureScripting",
            "Calamari.AzureResourceGroup",
            "Calamari.AzureAppService",
            "Calamari.GoogleCloudScripting",
            "Calamari.AzureWebApp"
        ];

        static IEnumerable<KeyValuePair<string, PackagePropertiesToTest>> SupportedPackages()
        {
            var isWindowsEnvValue = Environment.GetEnvironmentVariable("IS_WINDOWS");
            var isWindows = isWindowsEnvValue == null ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows) : bool.Parse(isWindowsEnvValue);
            var buildable = BuildableCalamariProjects.GetCalamariProjectsToBuild(isWindows);

            return PackagesWithDetails()
                   .Where(kvp => buildable.Contains(kvp.Key));
        }

        static IEnumerable<string> ExpectedPackages()
            => SupportedPackages().Select(kvp => kvp.Key);

        static IEnumerable<TestCaseData> ExpectedPackageArchitectureMappings()
            => SupportedPackages()
               .Select(kvp => new TestCaseData(kvp.Key, kvp.Value.Architectures).SetName($"Package_{kvp.Key}_HasExpectedArchitectures"));

        static IEnumerable<TestCaseData> ExpectedPackagesWithDotnetScript()
            => SupportedPackages()
               .Where(kvp => PackagesWithDotnetScript.Contains(kvp.Key))
               .Select(kvp => new TestCaseData(kvp.Key).SetName($"Package_{kvp.Key}_ContainsDotnetScript"));

        static IEnumerable<TestCaseData> ExpectedPackageNugetStatus()
            => SupportedPackages()
               .Select(kvp => new TestCaseData(kvp.Key, kvp.Value.IsNupkg).SetName($"Package {kvp.Key} Has Expected Nuget PackageFlag"));

        [SetUp]
        public void SetUp()
        {
            consolidatedFilePath = Environment.GetEnvironmentVariable("CONSOLIDATED_ZIP") ?? "";
            expectedVersion = Environment.GetEnvironmentVariable("EXPECTED_VERSION") ?? "";

            var indexLoader = new ConsolidatedPackageIndexLoader();
            using var fileStream = File.OpenRead(consolidatedFilePath);
            consolidatedPackageIndex = indexLoader.Load(fileStream);
        }

        [TestCaseSource(nameof(ExpectedPackageArchitectureMappings))]
        public void ConsolidatedPackageIndex_ContainsExpectedArchitecturesForAllPackages(string packageName, string[] expectedArchitectures)
        {
            var package = consolidatedPackageIndex.GetPackage(packageName);
            package.PlatformFiles.Keys.Should().BeEquivalentTo(expectedArchitectures);
        }

        [TestCaseSource(nameof(ExpectedPackages))]
        public void ConsolidatedPackageIndex_PackagesHaveCorrectVersion(string packageName)
        {
            var package = consolidatedPackageIndex.GetPackage(packageName);
            package.Version.Should().Be(expectedVersion);
        }

        [TestCaseSource(nameof(ExpectedPackageNugetStatus))]
        public void ConsolidatedPackageIndex_FlagsNugetPackagesCorrectly(string packageName, bool isNugetPackage)
        {
            var package = consolidatedPackageIndex.GetPackage(packageName);
            package.IsNupkg.Should().Be(isNugetPackage);
        }

        [TestCaseSource(nameof(ExpectedPackagesWithDotnetScript))]
        public void ConsolidatedPackageIndex_ContainsDotnetScriptForScriptingFlavours(string packageName)
        {
            var package = consolidatedPackageIndex.GetPackage(packageName);
            foreach (var (platform, fileTransfers) in package.PlatformFiles)
            {
                fileTransfers.Should().Contain(
                    ft => ft.Destination.Contains("dotnet-script/"),
                    $"{packageName} on {platform} should contain dotnet-script");
            }
        }
    }
}

