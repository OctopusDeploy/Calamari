using Autofac;
using Calamari.Hooks;

namespace Calamari.Aws.Module
{
    /// <summary>
    /// The script wrapper exposed by this module needs to be used for AWS script steps
    /// </summary>
    public class CalamariModule : Autofac.Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder
                .RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();
        }
    }
}
