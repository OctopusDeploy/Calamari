using System.Reflection;
using Autofac;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Tests.Autofac;

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
