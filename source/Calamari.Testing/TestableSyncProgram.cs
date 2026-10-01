using System;
using System.Linq;
using System.Reflection;
using Autofac;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.Testing;

/// <summary>
/// Builds a test DI container for a <see cref="CalamariFlavourProgram"/>-based (sync) flavour.
/// Use <see cref="For{TProgram}"/> to derive the configuration from a production program type.
/// </summary>
public class TestableSyncProgram
{
    readonly Action<ContainerBuilder, CommonOptions> _configure;

    TestableSyncProgram(Action<ContainerBuilder, CommonOptions> configure)
    {
        _configure = configure;
    }

    /// <summary>
    /// Creates a <see cref="TestableSyncProgram"/> that calls the same
    /// <c>ConfigureContainer</c> as the production <typeparamref name="TProgram"/>.
    /// </summary>
    public static TestableSyncProgram For<TProgram>() where TProgram : CalamariFlavourProgram
    {
        var ctor = typeof(TProgram)
            .GetConstructors(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .First(c => c.GetParameters() is { Length: 1 } p && p[0].ParameterType == typeof(ILog));

        var instance = (CalamariFlavourProgram)ctor.Invoke([ConsoleLog.Instance]);

        var configureMethod = instance.GetType()
            .GetMethod("ConfigureContainer", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return new TestableSyncProgram((b, o) => configureMethod.Invoke(instance, [b, o]));
    }

    public IContainer BuildTestContainer()
    {
        var options = CommonOptions.Parse(["version"]);
        var builder = new ContainerBuilder();
        _configure(builder, options);
        return builder.Build();
    }
}
