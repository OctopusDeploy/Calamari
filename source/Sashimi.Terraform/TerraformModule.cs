﻿using System;
using Autofac;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CloudTemplates;
using Sashimi.Terraform.ActionHandler;
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

            builder.RegisterType<TerraformApplyActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<TerraformDestroyActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<TerraformPlanActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<TerraformPlanDestroyActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
        }
    }
}
