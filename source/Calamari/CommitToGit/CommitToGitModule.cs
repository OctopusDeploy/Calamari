using Autofac;

namespace Calamari.CommitToGit
{
    public class CommitToGitModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<CommitToGitConfigFactory>().AsSelf().InstancePerLifetimeScope();
        }
    }
}
