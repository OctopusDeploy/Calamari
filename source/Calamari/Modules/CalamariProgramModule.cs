using Autofac;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;

namespace Calamari.Modules
{
    class CalamariProgramModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Program>()
                .WithParameter("displayName", "Calamari")
                .WithParameter("informationalVersion", typeof(Program).Assembly.GetInformationalVersion())
                .WithParameter("environmentInformation", EnvironmentHelper.SafelyGetEnvironmentInformation())
                .SingleInstance();
            builder.RegisterType<CombinedScriptEngine>().AsSelf();
        }
    }
}