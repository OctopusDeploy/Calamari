using System;
using Autofac;
using Calamari;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Tests.Shared.Server
{
    public abstract class BaseTest
    {
        public void TestActionHandler<TActionHandler, TCalamariProgram>(Action<TestActionHandlerContext<TCalamariProgram>> arrange, Action<IActionHandlerResult> assert) where TActionHandler : IActionHandler where TCalamariProgram : CalamariFlavourProgram
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(typeof(TActionHandler).Assembly);
            builder.RegisterModule<ServerModule>();

            var commandBuilder = new TestCalamariCommandBuilder<TCalamariProgram>();
            var context = new TestActionHandlerContext<TCalamariProgram>(commandBuilder);

            arrange(context);

            IActionHandlerResult result;
            using (var container = builder.Build())
            {
                var actionHandler = container.Resolve<TActionHandler>();

                commandBuilder.SetVariables(context.Variables);
                result = actionHandler.Execute(context);
            }

            assert(result);
        }
    }
}