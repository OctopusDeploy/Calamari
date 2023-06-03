using Autofac;
using Calamari.Deployment.Conventions;

namespace Calamari
{
    public class CalamariSharedModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<InstallConventionFactory>().As<IInstallConventionFactory>();
        }
    }
}