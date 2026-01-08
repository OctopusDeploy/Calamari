using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.AzureResourceGroup.Tests.Attributes;
using Calamari.AzureResourceGroup.Tests.Support;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Xunit.Sdk;

// ReSharper disable MethodHasAsyncOverload - File.ReadAllTextAsync does not exist for .net framework targets

namespace Calamari.AzureResourceGroup.Tests
{
    [Collection(nameof(AzureResourceGroupFixture))]
    public class AzureResourceGroupActionHandlerTests(AzureResourceGroupFixture resourceGroupFixture): CalamariTest
    {
        readonly AzureResourceGroupFixture resourceGroupFixture = resourceGroupFixture;

        [Fact]
        public async Task Deploy_with_template_in_package()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            await CommandTestBuilder.CreateAsync<DeployAzureResourceGroupCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Package");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplate", "azure_website_template.json");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplateParameters", "azure_website_params.json");
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        [Fact]
        public async Task Deploy_with_template_in_git_repository()
        {
            // For the purposes of ARM templates in Calamari, a template in a Git Repository
            // is equivalent to a template in a package, so we can just re-use the same
            // package in the test here, it's just the template source property that is
            // different.
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            await CommandTestBuilder.CreateAsync<DeployAzureResourceGroupCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "GitRepository");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplate", "azure_website_template.json");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplateParameters", "azure_website_params.json");
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        [Fact]
        public async Task Deploy_with_template_inline()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            var templateFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json"));
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_params.json"));
            var parameters = JObject.Parse(paramsFileContent)["parameters"].ToString();

            await CommandTestBuilder.CreateAsync<DeployAzureResourceGroupCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Inline");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupTemplate, File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json")));
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters, parameters);

                                                     context.WithFilesToCopy(packagePath);

                                                     AddTemplateFiles(context, templateFileContent, paramsFileContent);
                                                 })
                                    .Execute();
        }

        [Fact]
        [TestPlatforms(TestCategory.CompatibleOS.OnlyWindows)]
        public async Task Deploy_Ensure_Tools_Are_Configured()
        {
            if (ScriptingEnvironment.SafelyGetPowerShellVersion().Major < 5)
            {
                throw SkipException.ForSkip("This test requires PowerShell 5 or above.");
            }
            
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            var templateFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json"));
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_params.json"));
            var parameters = JObject.Parse(paramsFileContent)["parameters"].ToString();
            const string psScript = @"
$ErrorActionPreference = 'Continue'
az --version
Get-AzureEnvironment
az group list";

            await CommandTestBuilder.CreateAsync<DeployAzureResourceGroupCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Inline");
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupTemplate, File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json")));
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupTemplateParameters, parameters);
                                                     context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.Deploy, ScriptSyntax.PowerShell), psScript);
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PreDeploy, ScriptSyntax.CSharp), "Console.WriteLine(\"Hello from C#\");");

                                                     context.WithFilesToCopy(packagePath);

                                                     AddTemplateFiles(context, templateFileContent, paramsFileContent);
                                                 })
                                    .Execute();
        }

        void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            context.Variables.Add(AzureAccountVariables.SubscriptionId, resourceGroupFixture.SubscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, resourceGroupFixture.TenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, resourceGroupFixture.ClientId);
            context.Variables.Add(AzureAccountVariables.Password, resourceGroupFixture.ClientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupFixture.ResourceGroupName);
            context.Variables.Add("ResourceGroup", resourceGroupFixture.ResourceGroupName);
            context.Variables.Add("SKU", "Shared");
            //as we have a single resource group, we need to have unique web app name per test
            context.Variables.Add("WebSite", $"Calamari-{Guid.NewGuid():N}");
            context.Variables.Add("Location", resourceGroupFixture.ResourceGroupResource.Data.Location);
            //this is a storage account prefix, so just make it as random as possible
            //The names of the storage accounts are a max of 7 chars, so we generate a prefix of 17 chars (storage accounts have a max of 24)
            context.Variables.Add("AccountPrefix", AzureTestResourceHelpers.RandomName(length: 17));
        }

        private static void AddTemplateFiles(CommandTestBuilderContext context, string template, string parameters)
        {
            context.WithDataFile(template, "template.json");
            context.WithDataFile(parameters, "parameters.json");
        }
    }
}