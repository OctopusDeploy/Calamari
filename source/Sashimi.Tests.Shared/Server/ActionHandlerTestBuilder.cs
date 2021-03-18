using System;
using System.Collections.Generic;
using System.IO;
using Autofac;
using Calamari.Common;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using Sashimi.Server.Contracts.ActionHandlers;
using KnownVariables = Sashimi.Server.Contracts.KnownVariables;

namespace Sashimi.Tests.Shared.Server
{
    public static class ActionHandlerTestBuilder
    {
        public static ActionHandlerTestBuilder<TCalamari> CreateAsync<TActionHandler, TCalamari>()
            where TActionHandler : IActionHandler
            where TCalamari : CalamariFlavourProgramAsync
        {
            return new ActionHandlerTestBuilder<TCalamari>(typeof(TActionHandler));
        }

        public static ActionHandlerTestBuilder<TCalamari> CreateAsync<TCalamari>(Type actionHandlerType)
            where TCalamari : CalamariFlavourProgramAsync
        {
            return new ActionHandlerTestBuilder<TCalamari>(actionHandlerType);
        }

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

        public static TestActionHandlerContext<TCalamariProgram> WithFilesToCopy<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string path)
        {
            if (File.Exists(path))
            {
                context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, Path.GetDirectoryName(path));
            }
            else
            {
                context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, path);
            }

            context.Variables.Add("Octopus.Test.PackagePath", path);
            context.Variables.Add(KnownVariables.Action.Packages.FeedId, "FeedId");

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithPackage<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string packagePath, string packageId, string packageVersion)
        {
            context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, Path.GetDirectoryName(packagePath));
            context.Variables.Add(TentacleVariables.CurrentDeployment.PackageFilePath, packagePath);
            context.Variables.Add(KnownVariables.Action.Packages.PackageId, packageId);
            context.Variables.Add(KnownVariables.Action.Packages.Version, packageVersion);
            context.Variables.Add(KnownVariables.Action.Packages.FeedId, "FeedId");

            return context;
        }
    }

    public class ActionHandlerTestBuilder<TCalamariProgram>
    {
        readonly List<Action<TestActionHandlerContext<TCalamariProgram>>> arrangeActions;
        Action<TestActionHandlerResult>? assertAction;
        readonly Type actionHandlerType;

        public ActionHandlerTestBuilder(Type actionHandlerType)
        {
            this.actionHandlerType = actionHandlerType;
            arrangeActions = new List<Action<TestActionHandlerContext<TCalamariProgram>>>();
        }

        public ActionHandlerTestBuilder<TCalamariProgram> WithArrange(Action<TestActionHandlerContext<TCalamariProgram>> arrange)
        {
            arrangeActions.Add(arrange);
            return this;
        }

        public ActionHandlerTestBuilder<TCalamariProgram> WithAssert(Action<TestActionHandlerResult> assert)
        {
            assertAction = assert;
            return this;
        }

        public TestActionHandlerResult Execute(bool assertWasSuccess = true, bool runOutOfProc = false)
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(actionHandlerType.Assembly);
            builder.RegisterModule<ServerModule>();
            var container = builder.Build();
            var log = new SashimiInMemoryTaskLog();
            var context = new TestActionHandlerContext<TCalamariProgram>(log);

            foreach (var arrangeAction in arrangeActions)
            {
                arrangeAction?.Invoke(context);
            }

            TestActionHandlerResult result;
            using (container)
            {
                var actionHandler = (IActionHandler)container.Resolve(actionHandlerType);

                if (runOutOfProc)
                {
                    var currentInProcSetting = Environment.GetEnvironmentVariable(TestCalamariCommandBuilder<TCalamariProgram>.InProcOutProcOverride.EnvironmentVariable);

                    try
                    {
                        Environment.SetEnvironmentVariable(TestCalamariCommandBuilder<TCalamariProgram>.InProcOutProcOverride.EnvironmentVariable, TestCalamariCommandBuilder<TCalamariProgram>.InProcOutProcOverride.OutProcValue);

                        result = (TestActionHandlerResult)actionHandler.Execute(context, log);
                    }
                    finally
                    {
                        Environment.SetEnvironmentVariable(TestCalamariCommandBuilder<TCalamariProgram>.InProcOutProcOverride.EnvironmentVariable, currentInProcSetting);
                    }
                }
                else
                {
                    result = (TestActionHandlerResult)actionHandler.Execute(context, log);
                }

                Console.WriteLine(result.FullLog);
            }

            if (assertWasSuccess)
            {
                result.WasSuccessful.Should().BeTrue($"{actionHandlerType} execute result was unsuccessful.");
            }
            assertAction?.Invoke(result);

            return result;
        }
    }
}