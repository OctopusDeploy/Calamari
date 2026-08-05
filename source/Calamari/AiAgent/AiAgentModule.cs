using Autofac;
using Calamari.AiAgent.ClaudeCodeBehaviour;

namespace Calamari.AiAgent
{
    public class AiAgentModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<ClaudeSettingsWriter>();
        }
    }
}
