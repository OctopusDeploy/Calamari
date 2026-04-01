using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Testing;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests.Autofac;

[TestFixture]
public class CommandResolutionTests
{
    [Test]
    [Category("PlatformAgnostic")]
    public void AllPipelineCommandsCanBeConstructed()
    {
        var program = TestablePipelineProgram.For<Calamari.AzureResourceGroup.Program>();
        using var container = program.BuildTestContainer();

        var failures = new List<string>();
        foreach (var type in program.PipelineCommandTypes)
        {
            try
            {
                container.Resolve(type);
            }
            catch (Exception ex)
            {
                failures.Add($"'{type.Name}': {ex.Message}");
            }
        }

        Assert.That(failures, Is.Empty, "all pipeline commands must be constructable from the DI container");
    }
}
