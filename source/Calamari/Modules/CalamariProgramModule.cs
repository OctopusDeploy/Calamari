using System;
using Autofac;
using Autofac.Core;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Journal;
using Calamari.Hooks;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Scripting;

namespace Calamari.Modules
{
    class CalamariProgramModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DeploymentJournalWriter>().As<IDeploymentJournalWriter>();
            builder.RegisterType<CombinedScriptEngine>().AsSelf();
            
            builder
                .RegisterAssemblyTypes(this.GetType().Assembly)
                .AssignableTo<IScriptWrapper>()
                .As<IScriptWrapper>()
                .SingleInstance();
        }
    }
}