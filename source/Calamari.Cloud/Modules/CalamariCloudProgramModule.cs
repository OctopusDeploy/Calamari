using Autofac;
using Calamari.Integration.Scripting;
using Calamari.Util.Environments;

namespace Calamari.Cloud.Modules
{
    public class CalamariCloudProgramModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<Program>()
                .WithParameter("displayName", "Calamari.Cloud")
                .WithParameter("informationalVersion", typeof(Program).Assembly.GetInformationalVersion())
                .WithParameter("environmentInformation", EnvironmentHelper.SafelyGetEnvironmentInformation())
                .SingleInstance();
            builder.RegisterType<CombinedScriptEngine>().AsSelf();
        }
    }
}