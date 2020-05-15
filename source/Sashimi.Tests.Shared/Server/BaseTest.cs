using System;
using Autofac;
using Calamari;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Tests.Shared.Server
{
    public abstract class BaseTest
    {
        public void TestActionHandler<TCalamariProgram>(
            Type actionHandlerType, Action<TestActionHandlerContext<TCalamariProgram>> arrange, Action<TestActionHandlerResult> assert)
            where TCalamariProgram : CalamariFlavourProgram
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(actionHandlerType.Assembly);
            builder.RegisterModule<ServerModule>();

            var commandBuilder = new TestCalamariCommandBuilder<TCalamariProgram>();
            var context = new TestActionHandlerContext<TCalamariProgram>(commandBuilder);

            arrange(context);

            TestActionHandlerResult result;
            using (var container = builder.Build())
            {
                var actionHandler = (IActionHandler)container.Resolve(actionHandlerType);

                commandBuilder.SetVariables(context.Variables);
                result = (TestActionHandlerResult) actionHandler.Execute(context);
            }

            assert(result);
        }

        public void TestActionHandler<TActionHandler, TCalamariProgram>(Action<TestActionHandlerContext<TCalamariProgram>> arrange, Action<TestActionHandlerResult> assert) where TActionHandler : IActionHandler where TCalamariProgram : CalamariFlavourProgram
        {
            TestActionHandler(typeof(TActionHandler), arrange, assert);
        }
        
        
    }

    
}