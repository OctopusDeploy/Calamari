using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.AzureAppService.Tests.Autofac;

[TestFixture]
public class AutofacRegistrationTests
{
    [Test]
    [Category("PlatformAgnostic")]
    public void AllPipelineCommandsCanBeConstructed()
    {
        var program = TestablePipelineProgram.For<Calamari.AzureAppService.Program>();
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

        failures.Should().BeEmpty("all pipeline commands must be constructable from the DI container");
    }
}
