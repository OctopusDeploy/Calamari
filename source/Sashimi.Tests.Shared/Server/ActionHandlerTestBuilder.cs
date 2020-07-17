using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using Calamari;
using Calamari.Common.Variables;
using Calamari.Common;
using FluentAssertions;
using Octopus.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using KnownVariables = Sashimi.Server.Contracts.KnownVariables;

namespace Sashimi.Tests.Shared.Server
{
    public static class ActionHandlerTestBuilder
    {
        public static ActionHandlerTestBuilder<TCalamari> CreateAsync<TActionHandler, TCalamari>()
            where TActionHandler : IActionHandler
            where TCalamari : Calamari.CommonTemp.CalamariFlavourProgramAsync
        {
            return new ActionHandlerTestBuilder<TCalamari>(typeof(TActionHandler));
        }

        public static ActionHandlerTestBuilder<TCalamari> CreateAsync<TCalamari>(Type actionHandlerType)
            where TCalamari : Calamari.CommonTemp.CalamariFlavourProgramAsync
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

            context.Variables.Add(KnownVariables.Action.Packages.PackageId, path);
            context.Variables.Add(KnownVariables.Action.Packages.FeedId, "FeedId");

            return context;
        }

        public static TestActionHandlerContext<TCalamariProgram> WithPackage<TCalamariProgram>(this TestActionHandlerContext<TCalamariProgram> context, string packagePath)
        {
            context.Variables.Add(KnownVariables.OriginalPackageDirectoryPath, Path.GetDirectoryName(packagePath));
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(packagePath);
            var fileNameChunks = fileNameWithoutExtension.Split('.').Reverse().ToArray();
            string? packageVersion = null;
            var idx = 0;
            for (; idx < fileNameChunks.Length; idx++)
            {
                var fileNameChunk = fileNameChunks[idx];

                if (Int32.TryParse(fileNameChunk, out _))
                {
                    packageVersion = packageVersion == null
                        ? fileNameChunk
                        : $"{fileNameChunk}.{packageVersion}";

                    continue;
                }
                break;
            }

            string? packageId = null;
            for (; idx < fileNameChunks.Length; idx++)
            {
                var fileNameChunk = fileNameChunks[idx];

                packageId = packageId == null
                    ? fileNameChunk
                    : $"{fileNameChunk}.{packageId}";
            }

            context.Variables.Add(TentacleVariables.CurrentDeployment.PackageFilePath, packagePath);
            context.Variables.Add("Octopus.Action.Package.PackageId", packageId);
            context.Variables.Add("Octopus.Action.Package.PackageVersion", packageVersion);
            context.Variables.Add("Octopus.Action.Package.FeedId", "FeedId");

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

        public TestActionHandlerResult Execute(bool assertWasSuccess = true)
        {
            var builder = new ContainerBuilder();
            builder.RegisterAssemblyModules(actionHandlerType.Assembly);
            builder.RegisterModule<ServerModule>();
            var container = builder.Build();
            var context = new TestActionHandlerContext<TCalamariProgram>(container.Resolve<ILog>());

            foreach (var arrangeAction in arrangeActions)
            {
                arrangeAction?.Invoke(context);
            }

            TestActionHandlerResult result;
            using (container)
            {
                var actionHandler = (IActionHandler) container.Resolve(actionHandlerType);

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