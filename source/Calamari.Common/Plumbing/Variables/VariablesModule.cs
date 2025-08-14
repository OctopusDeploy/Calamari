using Autofac;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Plumbing.Variables
{
    public class VariablesModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<VariablesFactory>().AsSelf().SingleInstance();
            
            builder.Register(c => c.Resolve<VariablesFactory>().Create(c.Resolve<CommonOptions>())).As<IVariables>().SingleInstance();
            builder.Register(c => c.Resolve<VariablesFactory>().CreateNonSensitiveVariables(c.Resolve<CommonOptions>())).As<INonSensitiveVariables>().SingleInstance();
        }
    }
}