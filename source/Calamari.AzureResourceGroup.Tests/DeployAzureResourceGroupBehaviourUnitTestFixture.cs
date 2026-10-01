using System;
using System.Threading.Tasks;
using Azure.ResourceManager.Resources.Models;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests
{
    // Covers DeployAzureResourceGroupBehaviour's template-source resolution, parameter normalisation and
    // deployment mode/name logic with a mocked IAzureResourceGroupOperator + ITemplateService. These
    // scenarios previously only ran against real Azure in AzureResourceGroupActionHandlerFixture.
    [TestFixture]
    public class DeployAzureResourceGroupBehaviourUnitTestFixture
    {
        const string SubscriptionId = "sub-id";
        const string ResourceGroupName = "my-rg";

        ITemplateService templateService;
        IResourceGroupTemplateNormalizer normalizer;
        IAzureResourceGroupOperator resourceGroupOperator;
        DeployAzureResourceGroupBehaviour sut;

        [SetUp]
        public void SetUp()
        {
            templateService = Substitute.For<ITemplateService>();
            normalizer = Substitute.For<IResourceGroupTemplateNormalizer>();
            resourceGroupOperator = Substitute.For<IAzureResourceGroupOperator>();
            normalizer.Normalize(Arg.Any<string>()).Returns(ci => $"normalized:{ci.Arg<string>()}");
            sut = new DeployAzureResourceGroupBehaviour(templateService, normalizer, new InMemoryLog(), resourceGroupOperator);
        }

        [Test]
        public async Task PackageSource_DeploysSubstitutedTemplateAndNormalisedParameters()
        {
            templateService.GetSubstitutedTemplateContent("template.json", true, Arg.Any<IVariables>()).Returns("TEMPLATE");
            templateService.GetSubstitutedTemplateContent("params.json", true, Arg.Any<IVariables>()).Returns("PARAMS");

            var context = ContextFor("Package", deploymentMode: "Complete", extra: v =>
                                                                                   {
                                                                                       v.Add(SpecialVariables.Action.Azure.ResourceGroupTemplate, "template.json");
                                                                                       v.Add(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters, "params.json");
                                                                                   });

            await sut.Execute(context);

            await resourceGroupOperator.Received(1).Deploy(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                           Arg.Any<string>(), ArmDeploymentMode.Complete, "TEMPLATE", "normalized:PARAMS", Arg.Any<IVariables>());
        }

        [Test]
        public async Task GitRepositorySource_ResolvesTemplateTheSameWayAsPackage()
        {
            // GitRepository and Package take the identical "files in package or repository" branch, so the
            // cloud fixture needs no separate Git deploy test.
            templateService.GetSubstitutedTemplateContent("template.json", true, Arg.Any<IVariables>()).Returns("TEMPLATE");
            templateService.GetSubstitutedTemplateContent("params.json", true, Arg.Any<IVariables>()).Returns("PARAMS");

            var context = ContextFor("GitRepository", deploymentMode: "Complete", extra: v =>
                                                                                         {
                                                                                             v.Add(SpecialVariables.Action.Azure.ResourceGroupTemplate, "template.json");
                                                                                             v.Add(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters, "params.json");
                                                                                         });

            await sut.Execute(context);

            templateService.Received().GetSubstitutedTemplateContent("template.json", true, Arg.Any<IVariables>());
            await resourceGroupOperator.Received(1).Deploy(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                           Arg.Any<string>(), ArmDeploymentMode.Complete, "TEMPLATE", "normalized:PARAMS", Arg.Any<IVariables>());
        }

        [Test]
        public async Task InlineSource_ResolvesTemplateFromInlineFilesNotPackage()
        {
            templateService.GetSubstitutedTemplateContent("template.json", false, Arg.Any<IVariables>()).Returns("INLINE_TEMPLATE");
            templateService.GetSubstitutedTemplateContent("parameters.json", false, Arg.Any<IVariables>()).Returns("INLINE_PARAMS");

            var context = ContextFor("Inline", deploymentMode: "Complete");

            await sut.Execute(context);

            await resourceGroupOperator.Received(1).Deploy(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                           Arg.Any<string>(), ArmDeploymentMode.Complete, "INLINE_TEMPLATE", "normalized:INLINE_PARAMS", Arg.Any<IVariables>());
        }

        [Test]
        public async Task DeploymentName_UsesExplicitVariableWhenProvided()
        {
            templateService.GetSubstitutedTemplateContent(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IVariables>()).Returns("T");

            var context = ContextFor("Inline", deploymentMode: "Complete", extra: v =>
                                                                                 v.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentName, "explicit-deployment"));

            await sut.Execute(context);

            await resourceGroupOperator.Received(1).Deploy(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                           "explicit-deployment", Arg.Any<ArmDeploymentMode>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IVariables>());
        }

        [Test]
        public async Task DeploymentName_DerivedFromStepNameWhenNotProvided()
        {
            templateService.GetSubstitutedTemplateContent(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IVariables>()).Returns("T");

            var context = ContextFor("Inline", deploymentMode: "Complete", extra: v =>
                                                                                 v.Add(ActionVariables.Name, "My Deploy Step"));

            await sut.Execute(context);

            // FromStepName sanitises the step name and appends a random suffix, so assert the prefix.
            await resourceGroupOperator.Received(1).Deploy(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                           Arg.Is<string>(name => name.StartsWith("my-deploy-step-")), Arg.Any<ArmDeploymentMode>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IVariables>());
        }

        [TestCase("Complete", ArmDeploymentMode.Complete)]
        [TestCase("Incremental", ArmDeploymentMode.Incremental)]
        public async Task DeploymentMode_IsParsedFromVariable(string modeVariable, ArmDeploymentMode expectedMode)
        {
            templateService.GetSubstitutedTemplateContent(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<IVariables>()).Returns("T");

            var context = ContextFor("Inline", deploymentMode: modeVariable);

            await sut.Execute(context);

            await resourceGroupOperator.Received(1).Deploy(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                           Arg.Any<string>(), expectedMode, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IVariables>());
        }

        static RunningDeployment ContextFor(string templateSource, string deploymentMode, Action<IVariables> extra = null)
        {
            var variables = new CalamariVariables();

            // Dummy credentials - the operator that would use them to reach Azure is mocked in these tests.
            variables.Add(AzureAccountVariables.SubscriptionId, SubscriptionId);
            variables.Add(AzureAccountVariables.ClientId, "client-id");
            variables.Add(AzureAccountVariables.TenantId, "tenant-id");
            variables.Add(AzureAccountVariables.Password, "client-secret");

            variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
            variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, deploymentMode);
            variables.Add(SpecialVariables.Action.Azure.TemplateSource, templateSource);

            extra?.Invoke(variables);
            return new RunningDeployment("", variables);
        }
    }
}
