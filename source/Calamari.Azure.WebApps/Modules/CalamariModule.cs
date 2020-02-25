using Autofac;
using Calamari.HealthChecks;
using Calamari.Hooks;

namespace Calamari.Azure.WebApps.Modules
{
    /// <summary>
    /// The script wrapper exposed by this module needs to be used for Azure script steps
    /// </summary>
    public class CalamariModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();

            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IDoesDeploymentTargetTypeHealthChecks>()
                .As<IDoesDeploymentTargetTypeHealthChecks>()
                .InstancePerDependency();
        }
    }
}
