using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing;
using Calamari.Testing.Helpers;
using Calamari.Testing.Requirements;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Calamari.AzureResourceGroup.Tests
{
    [TestFixture]
    class AzureResourceGroupActionHandlerFixture
    {
        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;
        IResourceGroup resourceGroup;
        IAzure azure;
        
        static string GetTestPath(params string[] paths) => Path.Combine(TestEnvironment.CurrentWorkingDirectory ?? string.Empty, Path.Combine(paths));


        [OneTimeSetUp]
        public async Task Setup()
        {
            clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var resourceGroupName = SdkContext.RandomResourceName(nameof(AzureResourceGroupActionHandlerFixture), 60);

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId,
                                                                                      clientSecret,
                                                                                      tenantId,
                                                                                      AzureEnvironment.AzureGlobalCloud);

            azure = Microsoft.Azure.Management.Fluent.Azure
                             .Configure()
                             .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                             .Authenticate(credentials)
                             .WithSubscription(subscriptionId);

            resourceGroup = await azure.ResourceGroups
                                       .Define(resourceGroupName)
                                       .WithRegion(Region.USWest)
                                       .CreateAsync();
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            if (resourceGroup != null)
            {
                await azure.ResourceGroups.DeleteByNameAsync(resourceGroup.Name);
            }
        }

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

        [Test]
        [WindowsTest]
        [RequiresPowerShell5OrAbove]
        public async Task Deploy_Ensure_Tools_Are_Configured()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            var templateFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json"));
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_params.json"));
            var parameters = JObject.Parse(paramsFileContent)["parameters"].ToString();
            var psScript = @"
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
                                               context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, ScriptSyntax.FSharp), "printfn \"Hello from F#\"");

                                               context.WithFilesToCopy(packagePath);
                                               AddTemplateFiles(context, templateFileContent, paramsFileContent);
                                           })
                              .Execute();
        }

        private void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            context.Variables.Add(AzureAccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, tenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, clientId);
            context.Variables.Add(AzureAccountVariables.Password, clientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroup.Name);
            context.Variables.Add("ResourceGroup", resourceGroup.Name);
            context.Variables.Add("SKU", "Shared");
            context.Variables.Add("WebSite", SdkContext.RandomResourceName(String.Empty, 12));
            context.Variables.Add("Location", resourceGroup.RegionName);
            context.Variables.Add("AccountPrefix", SdkContext.RandomResourceName(String.Empty, 6));
        }

        private static void AddTemplateFiles(CommandTestBuilderContext context, string template, string parameters)
        {
            context.WithDataFile(template, "template.json");
            context.WithDataFile(parameters, "parameters.json");
        }
    }
}