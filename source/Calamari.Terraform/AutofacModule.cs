using Autofac;

namespace Calamari.Terraform
{
    public class AutofacModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<TerraformCliExecutor>().AsSelf();
        }
    }
}