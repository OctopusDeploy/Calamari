using System;
using System.Collections.Generic;
using System.Reflection;
using Autofac;
using Autofac.Features.Metadata;
using Calamari.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests;

[TestFixture]
public class AutofacRegistrationTests
{
    class TestableProgram : Program
    {
        public TestableProgram() : base(ConsoleLog.Instance) { }

        // Pin to the production assembly so the test assembly (and its StubCommand) isn't scanned
        protected override Assembly GetProgramAssemblyToRegister() => typeof(Program).Assembly;

        public IContainer BuildTestContainer()
        {
            var options = CommonOptions.Parse(["version"]);
            var builder = new ContainerBuilder();
            ConfigureContainer(builder, options);
            return builder.Build();
        }
    }

    [Test]
    [Category("PlatformAgnostic")]
    public void AllCommandsCanBeConstructed()
    {
        using var container = new TestableProgram().BuildTestContainer();

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
}
