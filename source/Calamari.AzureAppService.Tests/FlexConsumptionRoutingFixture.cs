using System.IO;
using Calamari.AzureAppService;
using Calamari.AzureAppService.Behaviors;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests;

[TestFixture]
public class FlexConsumptionRoutingFixture
{
    [TestCase("FlexConsumption", null, true)]
    [TestCase("flexconsumption", null, true)]      // case-insensitive
    [TestCase(null, "FC1", true)]                   // tier absent, detect by SKU name
    [TestCase(null, "fc1", true)]
    [TestCase("Dynamic", "Y1", false)]              // regular Consumption
    [TestCase("PremiumV3", "P1v3", false)]
    [TestCase(null, null, false)]                   // unknown -> non-Flex (falls back to ZipDeploy)
    public void IsFlexConsumptionTier_MatchesTierOrSkuName(string tier, string name, bool expected)
    {
        AzureAppServiceZipDeployBehaviour.IsFlexConsumptionTier(tier, name).Should().Be(expected);
    }

    [Test]
    public void OneDeployProvider_ConvertsNupkgToZipSibling()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var nupkg = Path.Combine(dir, "MyApp.1.0.0.nupkg");
            File.WriteAllText(nupkg, "content");

            var result = new OneDeployZipPackageProvider().ConvertToAzureSupportedFile(new FileInfo(nupkg)).GetAwaiter().GetResult();

            result.FullName.Should().Be(Path.Combine(dir, "MyApp.1.0.0.zip"));
            result.Exists.Should().BeTrue();
        }
        finally { Directory.Delete(dir, true); }
    }

    [Test]
    public void OneDeployProvider_PassesZipThroughUnchanged()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        try
        {
            var zip = Path.Combine(dir, "MyApp.1.0.0.zip");
            File.WriteAllText(zip, "content");

            var result = new OneDeployZipPackageProvider().ConvertToAzureSupportedFile(new FileInfo(zip)).GetAwaiter().GetResult();

            result.FullName.Should().Be(zip);
        }
        finally { Directory.Delete(dir, true); }
    }
}
