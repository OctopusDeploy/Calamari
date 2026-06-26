using System;
using System.Threading.Tasks;
using Azure.ResourceManager.Resources.Models;
using Calamari.AzureResourceGroup.Bicep;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests
{
    // Covers Bicep template-source resolution and the compile -> substitute -> deploy wiring of
    // DeployBicepTemplateBehaviour with a mocked IBicepTemplateBuilder (the az-cli step) + IAzureResourceGroupOperator.
    // These previously only ran against real Azure (and needed the az cli) in DeployAzureBicepTemplateCommandFixture.
    [TestFixture]
    public class DeployBicepTemplateBehaviourUnitTestFixture
    {
        const string SubscriptionId = "sub-id";
        const string ResourceGroupName = "my-rg";
        const string ResourceGroupLocation = "australiaeast";
        const string CompiledArmTemplatePath = "/working/ARMTemplate.json";

        IBicepTemplateBuilder bicepCompiler;
        ITemplateService templateService;
        IAzureResourceGroupOperator resourceGroupOperator;
        DeployBicepTemplateBehaviour sut;

        [SetUp]
        public void SetUp()
        {
            bicepCompiler = Substitute.For<IBicepTemplateBuilder>();
            templateService = Substitute.For<ITemplateService>();
            resourceGroupOperator = Substitute.For<IAzureResourceGroupOperator>();

            bicepCompiler.BuildArmTemplate(Arg.Any<string>(), Arg.Any<string>()).Returns(CompiledArmTemplatePath);
            // Must be valid JSON - BicepToArmParameterMapper.Map parses it.
            templateService.GetSubstitutedTemplateContent(CompiledArmTemplatePath, Arg.Any<bool>(), Arg.Any<IVariables>()).Returns("{}");

            sut = new DeployBicepTemplateBehaviour(bicepCompiler, templateService, resourceGroupOperator, new InMemoryLog());
        }

        [Test]
        public async Task PackageSource_CompilesBicepTemplateFromPackageAndDeploys()
        {
            var context = ContextFor("Package", v => v.Add(SpecialVariables.Action.Azure.BicepTemplate, "my-template.bicep"));

            await sut.Execute(context);

            bicepCompiler.Received(1).BuildArmTemplate(Arg.Any<string>(), "my-template.bicep");
            templateService.Received(1).GetSubstitutedTemplateContent(CompiledArmTemplatePath, true, Arg.Any<IVariables>());
            await resourceGroupOperator.Received(1).DeployCreatingResourceGroup(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                                                ResourceGroupLocation, Arg.Any<string>(), ArmDeploymentMode.Complete, "{}", Arg.Any<string>(), Arg.Any<IVariables>());
        }

        [Test]
        public async Task GitRepositorySource_ResolvesBicepTemplateTheSameWayAsPackage()
        {
            // GitRepository and Package take the identical branch - the cloud fixture needs no separate Git test.
            var context = ContextFor("GitRepository", v => v.Add(SpecialVariables.Action.Azure.BicepTemplate, "my-template.bicep"));

            await sut.Execute(context);

            bicepCompiler.Received(1).BuildArmTemplate(Arg.Any<string>(), "my-template.bicep");
            templateService.Received(1).GetSubstitutedTemplateContent(CompiledArmTemplatePath, true, Arg.Any<IVariables>());
        }

        [Test]
        public async Task InlineSource_CompilesDefaultBicepFileNotFromPackage()
        {
            var context = ContextFor("Inline");

            await sut.Execute(context);

            // Inline falls back to the default "template.bicep" file written to the working directory.
            bicepCompiler.Received(1).BuildArmTemplate(Arg.Any<string>(), "template.bicep");
            templateService.Received(1).GetSubstitutedTemplateContent(CompiledArmTemplatePath, false, Arg.Any<IVariables>());
        }

        [TestCase("Complete", ArmDeploymentMode.Complete)]
        [TestCase("Incremental", ArmDeploymentMode.Incremental)]
        public async Task DeploymentMode_IsParsedFromVariable(string modeVariable, ArmDeploymentMode expectedMode)
        {
            var context = ContextFor("Inline", deploymentMode: modeVariable);

            await sut.Execute(context);

            await resourceGroupOperator.Received(1).DeployCreatingResourceGroup(Arg.Any<IAzureAccount>(), SubscriptionId, ResourceGroupName,
                                                                                ResourceGroupLocation, Arg.Any<string>(), expectedMode, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<IVariables>());
        }

        static RunningDeployment ContextFor(string templateSource, Action<IVariables> extra = null, string deploymentMode = "Complete")
        {
            var variables = new CalamariVariables();

            variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            // Dummy credentials - the operator and compiler that would use them are mocked in these tests.
            variables.Add(AzureAccountVariables.SubscriptionId, SubscriptionId);
            variables.Add(AzureAccountVariables.ClientId, "client-id");
            variables.Add(AzureAccountVariables.TenantId, "tenant-id");
            variables.Add(AzureAccountVariables.Password, "client-secret");

            variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
            variables.Add(SpecialVariables.Action.Azure.ResourceGroupLocation, ResourceGroupLocation);
            variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, deploymentMode);
            variables.Add(SpecialVariables.Action.Azure.TemplateSource, templateSource);

            extra?.Invoke(variables);
            return new RunningDeployment("", variables);
        }
    }
}
