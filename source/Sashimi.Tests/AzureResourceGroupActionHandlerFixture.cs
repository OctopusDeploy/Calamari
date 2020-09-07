using System;
using System.IO;
using System.Threading.Tasks;
using Calamari.AzureResourceGroup;
using Calamari.Common.Features.Deployment;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Shared;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Sashimi.Tests.Shared;
using Sashimi.Tests.Shared.Server;

namespace Sashimi.AzureResourceGroup.Tests
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
        public void Deploy_with_template_in_package()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            ActionHandlerTestBuilder.CreateAsync<AzureResourceGroupActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Package");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplate", "azure_website_template.json");
                                                     context.Variables.Add("Octopus.Action.Azure.ResourceGroupTemplateParameters", "azure_website_params.json");
                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        [Test]
        public void Deploy_with_template_inline()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_params.json"));
            var parameters = JObject.Parse(paramsFileContent)["parameters"].ToString();

            ActionHandlerTestBuilder.CreateAsync<AzureResourceGroupActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Inline");
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplate, File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json")));
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplateParameters, parameters);

                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        [Test]
        [WindowsTest]
        [RequiresPowerShell5OrAboveAttribute]
        public void Deploy_Ensure_Tools_Are_Configured()
        {
            var packagePath = TestEnvironment.GetTestPath("Packages", "AzureResourceGroup");
            var paramsFileContent = File.ReadAllText(Path.Combine(packagePath, "azure_website_params.json"));
            var parameters = JObject.Parse(paramsFileContent)["parameters"].ToString();
            var psScript = @"
az --version
Get-AzureEnvironment
az group list";

            ActionHandlerTestBuilder.CreateAsync<AzureResourceGroupActionHandler, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     AddDefaults(context);
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupDeploymentMode, "Complete");
                                                     context.Variables.Add("Octopus.Action.Azure.TemplateSource", "Inline");
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplate, File.ReadAllText(Path.Combine(packagePath, "azure_website_template.json")));
                                                     context.Variables.Add(SpecialVariables.Action.AzureResourceGroup.ResourceGroupTemplateParameters, parameters);
                                                     context.Variables.Add(KnownVariables.Package.EnabledFeatures, KnownVariables.Features.CustomScripts);
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.Deploy, ScriptSyntax.PowerShell), psScript);
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PreDeploy, ScriptSyntax.CSharp), "Console.WriteLine(\"Hello from C#\");");
                                                     context.Variables.Add(KnownVariables.Action.CustomScripts.GetCustomScriptStage(DeploymentStages.PostDeploy, ScriptSyntax.FSharp), "printfn \"Hello from F#\"");

                                                     context.WithFilesToCopy(packagePath);
                                                 })
                                    .Execute();
        }

        void AddDefaults(TestActionHandlerContext<Program> context)
        {
            context.Variables.Add(Server.Contracts.KnownVariables.Account.AccountType, "AzureServicePrincipal");
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
    }
}