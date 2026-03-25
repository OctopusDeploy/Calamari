using Autofac;

namespace Calamari.Common.Features.Processes.ScriptIsolation;

public class ScriptIsolationModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<ScriptIsolationEnforcer>()
               .As<IScriptIsolationEnforcer>()
               .InstancePerLifetimeScope();
        builder.Register(_ => DefaultPathResolutionService.Instance)
               .As<IPathResolutionService>()
               .SingleInstance();
        builder.RegisterType<SystemMountedDrivesProvider>()
               .As<IMountedDrivesProvider>()
               .InstancePerLifetimeScope();
        builder.Register(_ => FileLockService.Instance)
               .As<IFileLockService>()
               .InstancePerLifetimeScope();
        builder.RegisterType<LockDirectoryFactory>()
               .As<ILockDirectoryFactory>()
               .InstancePerLifetimeScope();
        builder.RegisterType<RequestedLockOptionsFactory>()
               .AsSelf()
               .InstancePerLifetimeScope();
        builder.RegisterType<LockOptionsFactory>()
               .AsSelf()
               .InstancePerLifetimeScope();
    }
}
