using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests;

[TestFixture]
public class BicepTemplateCompilerFixture
{
    private readonly IBicepTemplateCompiler compiler = new ServiceCollection()
        .AddBicep()
        .BuildServiceProvider()
        .GetRequiredService<IBicepTemplateCompiler>();
    
    [Test]
    public void TestCompile()
    {
        var bicepPath = "./Packages/Bicep/container_app_sample.bicep";
        var expected = File.ReadAllText("./Packages/Bicep/container_app_sample.json");

        var got = compiler.Compile(bicepPath);
        
        Assert.AreEqual(expected, got);
    }
}