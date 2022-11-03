using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests;

[TestFixture]
public class BicepTemplateCompilerFixture
{
    private readonly IBicepTemplateCompiler compiler = new ServiceCollection()
        .AddSingleton<ICalamariFileSystem>(CalamariPhysicalFileSystem.GetPhysicalFileSystem())
        .AddBicep()
        .BuildServiceProvider()
        .GetRequiredService<IBicepTemplateCompiler>();
    
    [Test]
    public void TestCompile()
    {
        var template = File.ReadAllText("./Packages/Bicep/container_app_sample.bicep");
        var expected = File.ReadAllText("./Packages/Bicep/container_app_sample.json");

        var got = compiler.Compile(template);
        
        Assert.AreEqual(expected, got);
    }
}