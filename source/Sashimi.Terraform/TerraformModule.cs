using System;
using Autofac;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Terraform.CloudTemplates;

namespace Sashimi.Terraform
{
    /// <summary>
    /// This module is expected to register handlers that can parse parameters to return the module for
    /// DynamicForm, as well as any instances of IDeploymentActionDefinition.
    /// </summary>
    public class TerraformModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<TerraformJsonCloudTemplateHandler>().As<ICloudTemplateHandler>().SingleInstance();           
            builder.RegisterType<TerraformHclCloudTemplateHandler>().As<ICloudTemplateHandler>().SingleInstance();           
            
            builder.RegisterAssemblyTypes(ThisAssembly)
                .AssignableTo<IActionHandler>()
                .As<IActionHandler>()
                .AsSelf()
                .InstancePerLifetimeScope();
        }
    }
}
