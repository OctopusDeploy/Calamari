using Autofac;

namespace Calamari.Common.Features.Substitutions
{
    public class SubstitutionsModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<GlobSubstituteFileMatcher>().As<ISubstituteFileMatcher>().InstancePerLifetimeScope();
            
            //all variables
            builder.RegisterType<SubstituteInFiles>().As<ISubstituteInFiles>().InstancePerLifetimeScope();
            builder.RegisterType<FileSubstituter>().As<IFileSubstituter>().InstancePerLifetimeScope();

            //non-sensitive variables
            builder.RegisterType<NonSensitiveSubstituteInFiles>().As<INonSensitiveSubstituteInFiles>().InstancePerLifetimeScope();
            builder.RegisterType<NonSensitiveFileSubstituter>().As<INonSensitiveFileSubstituter>().InstancePerLifetimeScope();
        }
    }
}