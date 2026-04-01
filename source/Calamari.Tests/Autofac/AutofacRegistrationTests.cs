using System;
using System.Collections.Generic;
using Autofac;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Testing;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Autofac;

[TestFixture]
public class AutofacRegistrationTests
{
    [Test]
    [Category("PlatformAgnostic")]
    public void AllCommandsCanBeConstructed()
    {
        using var container = TestableSyncProgram.For<Calamari.Program>().BuildTestContainer();

        var commands = container.Resolve<IEnumerable<Meta<Lazy<ICommandWithArgs>, CommandMeta>>>();

        var failures = new List<string>();
        foreach (var command in commands)
        {
            try
            {
                var _ = command.Value.Value;
            }
            catch (Exception ex)
            {
                failures.Add($"'{command.Metadata.Name}': {ex.Message}");
            }
        }

        failures.Should().BeEmpty("all commands must be constructable from the DI container");
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void AllPipelineCommandsCanBeConstructed()
    {
        var program = new TestablePipelineProgram(typeof(Calamari.Program).Assembly);
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
