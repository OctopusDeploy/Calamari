using Autofac;
using Calamari.Hooks;

namespace Calamari.Azure
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
        }
    }
}
