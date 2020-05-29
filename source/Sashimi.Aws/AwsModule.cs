using Autofac;
using Sashimi.Aws.ActionHandler;
using Sashimi.Aws.CloudTemplates;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CloudTemplates;

namespace Sashimi.Aws
{
    /// <summary>
    /// This module is expected to register handlers that can parse parameters to return the module for
    /// DynamicForm, as well as any instances of IDeploymentActionDefinition.
    /// </summary>
    public class AwsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            base.Load(builder);

            builder.RegisterType<CloudFormationJsonCloudTemplateHandler>().As<ICloudTemplateHandler>().SingleInstance();
            builder.RegisterType<CloudFormationYamlCloudTemplateHandler>().As<ICloudTemplateHandler>().SingleInstance();

            builder.RegisterType<AwsUploadS3ActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<AwsRunScriptActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<AwsRunCloudFormationActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<AwsDeleteCloudFormationActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
            builder.RegisterType<AwsApplyCloudFormationChangeSetActionHandler>().As<IActionHandler>().AsSelf().InstancePerLifetimeScope();
        }
    }
}
