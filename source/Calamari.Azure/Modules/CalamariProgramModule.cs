using Autofac;
using Calamari.Util.Environments;

namespace Calamari.Azure.Modules
{
    class CalamariProgramModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Program>()
                .WithParameter("displayName", "Calamari.Azure")
                .WithParameter("informationalVersion", typeof(Program).Assembly.GetInformationalVersion())
                .WithParameter("environmentInformation", EnvironmentHelper.SafelyGetEnvironmentInformation())
                .SingleInstance();
        }
    }
}