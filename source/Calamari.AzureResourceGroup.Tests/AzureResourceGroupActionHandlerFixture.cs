using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Calamari.Azure;
using Calamari.CloudAccounts;
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

namespace Calamari.AzureResourceGroup.Tests
{
    [TestFixture]
    class AzureResourceGroupActionHandlerFixture
    {
        string clientId;
        string clientSecret;
        string tenantId;
        string subscriptionId;
        static readonly CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();
        readonly CancellationToken cancellationToken = CancellationTokenSource.Token;

        ArmClient armClient;
        SubscriptionResource subscriptionResource;
        ResourceGroupResource resourceGroupResource;
        string resourceGroupName;

        [OneTimeSetUp]
        public async Task Setup()
        {
            var resourceManagementEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ResourceManagementEndPoint) ?? DefaultVariables.ResourceManagementEndpoint;
            var activeDirectoryEndpointBaseUri =
                Environment.GetEnvironmentVariable(AccountVariables.ActiveDirectoryEndPoint) ?? DefaultVariables.ActiveDirectoryEndpoint;

            clientId = await ExternalVariables.Get(ExternalVariable.AzureAksSubscriptionClientId, cancellationToken);
            clientSecret = await ExternalVariables.Get(ExternalVariable.AzureAksSubscriptionPassword, cancellationToken);
            tenantId = await ExternalVariables.Get(ExternalVariable.AzureAksSubscriptionTenantId, cancellationToken);
            subscriptionId = await ExternalVariables.Get(ExternalVariable.AzureAksSubscriptionId, cancellationToken);

            var resourceGroupLocation = Environment.GetEnvironmentVariable("AZURE_NEW_RESOURCE_REGION") ?? RandomAzureRegion.GetRandomRegionWithExclusions();

            resourceGroupName = AzureTestResourceHelpers.GetResourceGroupName();

            var servicePrincipalAccount = new AzureServicePrincipalAccount(subscriptionId,
                                                                           clientId,
                                                                           tenantId,
                                                                           clientSecret,
                                                                           "AzureGlobalCloud",
                                                                           resourceManagementEndpointBaseUri,
                                                                           activeDirectoryEndpointBaseUri);

            armClient = servicePrincipalAccount.CreateArmClient(retryOptions =>
                                                                {
                                                                    retryOptions.MaxRetries = 5;
                                                                    retryOptions.Mode = RetryMode.Exponential;
                                                                    retryOptions.Delay = TimeSpan.FromSeconds(2);
                                                                    retryOptions.NetworkTimeout = TimeSpan.FromSeconds(200);
                                                                });

            //create the resource group
            subscriptionResource = armClient.GetSubscriptionResource(SubscriptionResource.CreateResourceIdentifier(subscriptionId));

            var response = await subscriptionResource
                                 .GetResourceGroups()
                                 .CreateOrUpdateAsync(WaitUntil.Completed,
                                                      resourceGroupName,
                                                      new ResourceGroupData(new AzureLocation(resourceGroupLocation))
                                                      {
                                                          Tags =
                                                          {
                                                              [AzureTestResourceHelpers.ResourceGroupTags.LifetimeInDaysKey] = AzureTestResourceHelpers.ResourceGroupTags.LifetimeInDaysValue,
                                                              [AzureTestResourceHelpers.ResourceGroupTags.SourceKey] = AzureTestResourceHelpers.ResourceGroupTags.SourceValue
                                                          }
                                                      });

            resourceGroupResource = response.Value;
        }

        [OneTimeTearDown]
        public async Task Cleanup()
        {
            await armClient.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(subscriptionId, resourceGroupName))
                           .DeleteAsync(WaitUntil.Started);
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

        private void AddDefaults(CommandTestBuilderContext context)
        {
            context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
            context.Variables.Add(AzureAccountVariables.SubscriptionId, subscriptionId);
            context.Variables.Add(AzureAccountVariables.TenantId, tenantId);
            context.Variables.Add(AzureAccountVariables.ClientId, clientId);
            context.Variables.Add(AzureAccountVariables.Password, clientSecret);
            context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
            context.Variables.Add("ResourceGroup", resourceGroupName);
            context.Variables.Add("SKU", "Shared");
            //as we have a single resource group, we need to have unique web app name per test
            context.Variables.Add("WebSite", $"Calamari-${Guid.NewGuid():N}");
            context.Variables.Add("Location", resourceGroupResource.Data.Location);
            //this is a storage account prefix, so just make it as random as possible. The first character must be a letter, so just set to c (for calamari).
            context.Variables.Add("AccountPrefix", AzureTestResourceHelpers.RandomName("c", 5));
        }

        private static void AddTemplateFiles(CommandTestBuilderContext context, string template, string parameters)
        {
            context.WithDataFile(template, "template.json");
            context.WithDataFile(parameters, "parameters.json");
        }
    }
}