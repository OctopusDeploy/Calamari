using System;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.AzureScripting.Tests
{
    [TestFixture]
    public class AzureActionHandlerExtensionsFixture
    {
        [Test]
        public void AzureCmdletsToolAddedWhenBundledModulesTrue()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModules, Boolean.TrueString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.Received().WithTool(AzureTools.AzureCmdlets);
        }

        [Test]
        public void AzureCmdletsToolExcludedWhenBundledModulesFalse()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModules, Boolean.FalseString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.DidNotReceive().WithTool(AzureTools.AzureCmdlets);
        }

        [Test]
        public void AzureCmdletsToolAddedWhenLegacyVariableIsTrue()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModulesLegacy, Boolean.TrueString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.Received().WithTool(AzureTools.AzureCmdlets);
        }

        [Test]
        public void AzureCmdletsToolExcludedWhenLegacyVariableIsFalse()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModulesLegacy, Boolean.FalseString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.DidNotReceive().WithTool(AzureTools.AzureCmdlets);
        }

        [Test]
        public void AzureCmdletsWillIgnoreLegacyVariableWhenBundledModulesFalse()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModulesLegacy, Boolean.TrueString);
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModules, Boolean.FalseString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.DidNotReceive().WithTool(AzureTools.AzureCmdlets);
        }

        [Test]
        public void AzureCmdletsWillIgnoreLegacyVariableWhenBundledModulesTrueAndLegacyIsFalse()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModulesLegacy, Boolean.FalseString);
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModules, Boolean.TrueString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.Received().WithTool(AzureTools.AzureCmdlets);
        }

        [Test]
        public void AzureCmdletsWillNotBeAddedWhenLegacyAndBundledModulesSetToFalse()
        {
            var builder = Substitute.For<ICalamariCommandBuilder>();
            var context = Substitute.For<IActionHandlerContext>();

            var variables = new TestVariableDictionary();
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModulesLegacy, Boolean.FalseString);
            variables.Set(SpecialVariables.Action.Azure.UseBundledAzureModules, Boolean.FalseString);
            context.Variables.Returns(variables);

            builder.WithAzureTools(context, Substitute.For<ITaskLog>());
            builder.DidNotReceive().WithTool(AzureTools.AzureCmdlets);
        }
    }
}