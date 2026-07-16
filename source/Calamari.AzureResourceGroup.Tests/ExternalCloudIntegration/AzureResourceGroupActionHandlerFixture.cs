using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Azure;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

// ReSharper disable MethodHasAsyncOverload - File.ReadAllTextAsync does not exist for .net framework targets

namespace Calamari.AzureResourceGroup.Tests.ExternalCloudIntegration
{
    [TestFixture]
    class AzureResourceGroupActionHandlerFixture : AzureResourceGroupCloudTestBase
    {
        [Test]
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

        [Test]
        [WindowsTest]
        public async Task Deploy_Ensure_Tools_Are_Configured()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            var templateFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json"));
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_params.json"));
            var parameters = JObject.Parse(paramsFileContent)["parameters"].ToString();
            const string psScript = @"
$ErrorActionPreference = 'Continue'
az --version
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
            context.Variables.Add(AzureAccountVariables.SubscriptionId, SubscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, TenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, ClientId);
            context.Variables.Add(AzureAccountVariables.Password, ClientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, ResourceGroupName);
            context.Variables.Add("ResourceGroup", ResourceGroupName);
            context.Variables.Add("SKU", "Shared");
            //as we have a single resource group, we need to have unique web app name per test
            context.Variables.Add("WebSite", $"Calamari-{Guid.NewGuid():N}");
            context.Variables.Add("Location", ResourceGroupResource.Data.Location);
            //this is a storage account prefix, so just make it as random as possible
            //The names of the storage accounts are a max of 7 chars, so we generate a prefix of 17 chars (storage accounts have a max of 24)
            context.Variables.Add("AccountPrefix", AzureTestResourceHelpers.RandomName(length: 17));
        }

        static void AddTemplateFiles(CommandTestBuilderContext context, string template, string parameters)
        {
            context.WithDataFile(template, "template.json");
            context.WithDataFile(parameters, "parameters.json");
        }
    }
}
