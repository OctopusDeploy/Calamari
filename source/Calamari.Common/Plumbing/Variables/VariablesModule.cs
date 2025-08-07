using Autofac;
using Calamari.Common.Plumbing.Commands;

namespace Calamari.Common.Plumbing.Variables
{
    public class VariablesModule : Module
    {
        readonly CommonOptions options;

        public VariablesModule(CommonOptions options)
        {
            this.options = options;
        }
        
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<VariablesFactory>().AsSelf().SingleInstance();
            
            builder.Register(c => c.Resolve<VariablesFactory>().Create(options)).As<IVariables>().SingleInstance();
            builder.Register(c => c.Resolve<VariablesFactory>().CreateNonSensitiveOnlyVariables(options)).As<INonSensitiveOnlyVariables>().SingleInstance();
        }
    }
}