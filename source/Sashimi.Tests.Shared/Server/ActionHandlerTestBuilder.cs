using System;
using System.Collections.Generic;
using System.IO;
using Autofac;
using Calamari;
using Calamari.Tests.Shared;
using FluentAssertions;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Tests.Shared.Server
{
    public static class ActionHandlerTestBuilder
    {
        public static ActionHandlerTestBuilder<TCalamari> Create<TActionHandler, TCalamari>() 
            where TActionHandler : IActionHandler
            where TCalamari : CalamariFlavourProgram
        {
            return new ActionHandlerTestBuilder<TCalamari>(typeof(TActionHandler));
        }
        
        public static ActionHandlerTestBuilder<TCalamari> Create<TCalamari>(Type actionHandlerType) 
            where TCalamari : CalamariFlavourProgram
        {
            return new ActionHandlerTestBuilder<TCalamari>(actionHandlerType);
        }

        public static TestActionHandlerContext<TCalamariProgram> WithPackage<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string path)
            where TCalamariProgram : CalamariFlavourProgram
        {
            context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, Path.GetDirectoryName(path));
            context.Variables.Add(KnownVariables.Action.Packages.PackageId, path);
            context.Variables.Add(KnownVariables.Action.Packages.FeedId, "FeedId");

            return context;
        }
    }
    
    public class ActionHandlerTestBuilder<TCalamariProgram> 
        where TCalamariProgram : CalamariFlavourProgram
    {
        List<Action<TestActionHandlerContext<TCalamariProgram>>>? arrangeActions;
        Action<TestActionHandlerResult>? assertAction;
        Type actionHandlerType;

        public ActionHandlerTestBuilder(Type actionHandlerType)
        {
            this.actionHandlerType = actionHandlerType;
            arrangeActions = new List<Action<TestActionHandlerContext<TCalamariProgram>>>();
        }

        public ActionHandlerTestBuilder<TCalamariProgram> WithArrange(Action<TestActionHandlerContext<TCalamariProgram>> arrange)
        {
            arrangeActions!.Add(arrange);
            return this;
        }

        public ActionHandlerTestBuilder<TCalamariProgram> WithAssert(Action<TestActionHandlerResult> assert)
        {
            assertAction = assert;
            return this;
        }

        public TestActionHandlerResult Execute(bool assertWasSuccess = true)
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(actionHandlerType.Assembly);
            builder.RegisterModule<ServerModule>();
            var container = builder.Build();
            var commandBuilder = new TestCalamariCommandBuilder<TCalamariProgram>();
            var context = new TestActionHandlerContext<TCalamariProgram>(commandBuilder, container.Resolve<Octopus.Diagnostics.ILog>());

            foreach (var arrangeAction in arrangeActions!)
            {
                arrangeAction?.Invoke(context);    
            }

            TestActionHandlerResult result;
            using (container)
            {
                var actionHandler = (IActionHandler) container.Resolve(actionHandlerType);

                commandBuilder.SetVariables(context.Variables);

                result = (TestActionHandlerResult) actionHandler.Execute(context);
            }

            if (assertWasSuccess)
            {
                result.WasSuccessful.Should().BeTrue($"{actionHandlerType} execute result was unsuccessful");
            }
            assertAction?.Invoke(result);

            return result;
        }
    }
}