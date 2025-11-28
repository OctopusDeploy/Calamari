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
    struct PackagePropertiesToTest
    {
        public string[] Architectures { get; }
        public bool IsNupkg { get; }

        public PackagePropertiesToTest(string[] architectures, bool isNupkg)
        {
            Architectures = architectures;
            IsNupkg = isNupkg;
        }
    }

    [TestFixture]
    public class ConsolidationVerificationTests
    {
        string consolidatedFilePath = "";
        IConsolidatedPackageIndex consolidatedPackageIndex;
        string expectedVersion = "";

        static readonly string[] NetCoreArchitectures =
        {
            "linux-arm",
            "linux-arm64",
            "linux-x64",
            "osx-x64",
            "win-x64"
        };

        static readonly string[] WindowsOnlyArchitectures =
        {
            "netfx"
        };

        static readonly string[] AllArchitectures = NetCoreArchitectures.Concat(WindowsOnlyArchitectures).ToArray();

        static Dictionary<string, PackagePropertiesToTest> PackagesWithDetails(bool isWindows)
        {
            return new Dictionary<string, PackagePropertiesToTest>
            {
                { "Calamari", new PackagePropertiesToTest(AllArchitectures , true) },
                { "Calamari.Cloud", new PackagePropertiesToTest(WindowsOnlyArchitectures, true) },
                { "Calamari.AzureServiceFabric", new PackagePropertiesToTest(new[] { "netfx", "win-x64" }, false) },
                { "Calamari.AzureAppService", new PackagePropertiesToTest(isWindows ? AllArchitectures : NetCoreArchitectures, false) },
                { "Calamari.AzureResourceGroup", new PackagePropertiesToTest(isWindows ? AllArchitectures : NetCoreArchitectures, false) },
                { "Calamari.GoogleCloudScripting", new PackagePropertiesToTest(isWindows ? AllArchitectures : NetCoreArchitectures, false) },
                { "Calamari.AzureScripting", new PackagePropertiesToTest(isWindows ? AllArchitectures : NetCoreArchitectures, false) },
                { "Calamari.AzureWebApp", new PackagePropertiesToTest(isWindows ? AllArchitectures : NetCoreArchitectures, false) },
                { "Calamari.Terraform", new PackagePropertiesToTest(isWindows ? AllArchitectures : NetCoreArchitectures, false) }
            };
        }

        static bool PackageSupported(string packageId, bool isWindows)
        {
            if (isWindows)
            {
                // Windows supports everything
                return true;
            }

            return CalamariPackages.CrossPlatformFlavours.Contains(packageId) || packageId == "Calamari" || packageId == "Calamari.Cloud";
        }

        static IEnumerable<string> ExpectedPackages()
        {
            var isWindowsEnvValue = Environment.GetEnvironmentVariable("IS_WINDOWS");

            var isWindows = isWindowsEnvValue == null ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows) : bool.Parse(isWindowsEnvValue);

            return PackagesWithDetails(isWindows)
                   .Where(kvp => PackageSupported(kvp.Key, isWindows))
                   .Select(kvp => kvp.Key);
        }

        static IEnumerable<TestCaseData> ExpectedPackageArchitectureMappings()
        {
            var isWindowsEnvValue = Environment.GetEnvironmentVariable("IS_WINDOWS");

            var isWindows = isWindowsEnvValue == null ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows) : bool.Parse(isWindowsEnvValue);

            return PackagesWithDetails(isWindows)
                   .Where(kvp => PackageSupported(kvp.Key, isWindows))
                   .Select(kvp => new TestCaseData(kvp.Key, kvp.Value.Architectures).SetName($"Package_{kvp.Key}_HasExpectedArchitectures"));
        }

        static IEnumerable<TestCaseData> ExpectedPackageNugetStatus()
        {
            var isWindowsEnvValue = Environment.GetEnvironmentVariable("IS_WINDOWS");

            var isWindows = isWindowsEnvValue == null ? RuntimeInformation.IsOSPlatform(OSPlatform.Windows) : bool.Parse(isWindowsEnvValue);

            return PackagesWithDetails(isWindows)
                .Where(kvp => PackageSupported(kvp.Key, isWindows))
                .Select(kvp => new TestCaseData(kvp.Key, kvp.Value.IsNupkg).SetName($"Package {kvp.Key} Has Expected Nuget PackageFlag"));

        }

        [SetUp]
        public void SetUp()
        {
            consolidatedFilePath = Environment.GetEnvironmentVariable("CONSOLIDATED_ZIP") ?? "";
            expectedVersion = Environment.GetEnvironmentVariable("EXPECTED_VERSION") ?? "";

            var indexLoader = new ConsolidatedPackageIndexLoader();
            using (var fileStream = File.OpenRead(consolidatedFilePath))
            {
                consolidatedPackageIndex = indexLoader.Load(fileStream);
            }
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
    }
}
