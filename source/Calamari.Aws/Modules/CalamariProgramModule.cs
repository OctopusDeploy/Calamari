using Autofac;
using Calamari.Util.Environments;

namespace Calamari.Aws.Modules
{
    class CalamariProgramModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Program>()
                .WithParameter("displayName", "Calamari.Aws")
                .WithParameter("informationalVersion", typeof(Program).Assembly.GetInformationalVersion())
                .WithParameter("environmentInformation", EnvironmentHelper.SafelyGetEnvironmentInformation())
                .SingleInstance();
        }
    }
}