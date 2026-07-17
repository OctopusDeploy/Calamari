using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using Calamari.Common;
using Calamari.Common.Plumbing.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Testing;

/// <summary>
/// Builds a test DI container for a <see cref="CalamariFlavourProgramAsync"/>-based flavour,
/// registering all non-abstract <see cref="PipelineCommand"/> types found in the supplied assemblies.
/// Use <see cref="For{TProgram}"/> to derive the assembly set from a production program type,
/// or supply assemblies directly via the constructor.
/// </summary>
public class TestablePipelineProgram : CalamariFlavourProgramAsync
{
    readonly Assembly[] _assemblies;

    public TestablePipelineProgram(params Assembly[] assemblies) : base(ConsoleLog.Instance)
    {
        _assemblies = assemblies;
    }

    /// <summary>
    /// Creates a <see cref="TestablePipelineProgram"/> that uses the same assembly set as the
    /// production <typeparamref name="TProgram"/> (whatever its
    /// <c>GetProgramAssembliesToRegister()</c> returns).
    /// </summary>
    public static TestablePipelineProgram For<TProgram>()
        where TProgram : CalamariFlavourProgramAsync
    {
        var instance = (CalamariFlavourProgramAsync)Activator.CreateInstance(typeof(TProgram), ConsoleLog.Instance)!;
        return FromProgram(instance);
    }

    /// <summary>
    /// Creates a <see cref="TestablePipelineProgram"/> using the assembly set returned by the
    /// supplied production program instance.
    /// </summary>
    public static TestablePipelineProgram FromProgram(CalamariFlavourProgramAsync productionProgram)
    {
        var method = typeof(CalamariFlavourProgramAsync)
            .GetMethod("GetProgramAssembliesToRegister", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var assemblies = ((IEnumerable<Assembly>)method.Invoke(productionProgram, null)!).ToArray();
        return new TestablePipelineProgram(assemblies);
    }

    protected override IEnumerable<Assembly> GetProgramAssembliesToRegister() => _assemblies;

    public IEnumerable<Type> PipelineCommandTypes =>
        _assemblies
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(PipelineCommand).IsAssignableFrom(t) && !t.IsAbstract);

    public IContainer BuildTestContainer()
    {
        var options = CommonOptions.Parse(["version"]);
        var builder = new ContainerBuilder();
        ConfigureContainer(builder, options);

        builder.RegisterAssemblyTypes(_assemblies)
               .AssignableTo<PipelineCommand>()
               .Where(t => !t.IsAbstract)
               .AsSelf();

        return builder.Build();
    }
}
