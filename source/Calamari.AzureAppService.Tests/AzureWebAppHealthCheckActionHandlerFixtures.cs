﻿#nullable disable
using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Calamari.Azure;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Tests.Shared;
using FluentAssertions;
using Microsoft.Azure.Management.AppService.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Core;
using NUnit.Framework;
using OperatingSystem = Microsoft.Azure.Management.AppService.Fluent.OperatingSystem;

namespace Calamari.AzureAppService.Tests
{
    [TestFixture]
    class AzureWebAppHealthCheckActionHandlerFixtures
    {
        [Test]
        public async Task WebApp_Is_Found()
        {
            var resourceGroupName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            var credentials = SdkContext.AzureCredentialsFactory.FromServicePrincipal(clientId,
                                                                                      clientSecret,
                                                                                      tenantId,
                                                                                      AzureEnvironment.AzureGlobalCloud);

            var azure = Microsoft.Azure.Management.Fluent.Azure
                                 .Configure()
                                 .WithLogLevel(HttpLoggingDelegatingHandler.Level.Basic)
                                 .Authenticate(credentials)
                                 .WithSubscription(subscriptionId);

            IResourceGroup resourceGroup = null;
            try
            {
                resourceGroup = await azure.ResourceGroups
                                           .Define(resourceGroupName)
                                           .WithRegion(Region.USWest)
                                           .CreateAsync();

                var appServicePlan = await azure.AppServices.AppServicePlans
                                                .Define(SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60))
                                                .WithRegion(resourceGroup.Region)
                                                .WithExistingResourceGroup(resourceGroup)
                                                .WithPricingTier(PricingTier.BasicB1)
                                                .WithOperatingSystem(OperatingSystem.Windows)
                                                .CreateAsync();

                var webAppName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
                await azure.WebApps
                           .Define(webAppName)
                           .WithExistingWindowsPlan(appServicePlan)
                           .WithExistingResourceGroup(resourceGroup)
                           .WithRuntimeStack(WebAppRuntimeStack.NETCore)
                           .CreateAsync();

                await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                        .WithArrange(context =>
                                                     {
                                                         context.Variables.Add(AccountVariables.SubscriptionId,
                                                                               subscriptionId);
                                                         context.Variables.Add(AccountVariables.TenantId,
                                                                               tenantId);
                                                         context.Variables.Add(AccountVariables.ClientId,
                                                                               clientId);
                                                         context.Variables.Add(AccountVariables.Password,
                                                                               clientSecret);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, resourceGroupName);
                                                         context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, webAppName);
                                                         context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
                                                     })
                                        .WithAssert(result => result.WasSuccessful.Should().BeTrue())
                                        .Execute();
            }
            finally
            {
                if (resourceGroup != null)
                    azure.ResourceGroups.DeleteByNameAsync(resourceGroupName).Ignore();
            }
        }

        [Test]
        public async Task WebApp_Is_Not_Found()
        {
            var randomName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                    .WithArrange(context =>
                                                 {
                                                     context.Variables.Add(AccountVariables.SubscriptionId,
                                                                           subscriptionId);
                                                     context.Variables.Add(AccountVariables.TenantId,
                                                                           tenantId);
                                                     context.Variables.Add(AccountVariables.ClientId,
                                                                           clientId);
                                                     context.Variables.Add(AccountVariables.Password,
                                                                           clientSecret);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, randomName);
                                                     context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, randomName);
                                                     context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
                                                 })
                                    .WithAssert(result => result.WasSuccessful.Should().BeFalse())
                                    .Execute(false);
        }
    }

    [TestFixture]
    class AzureWebAppHealthCheckActionHandlerProxyFixture
    {
        const string NonExistentProxyHostname = "non-existent-proxy.local";
        const int NonExistentProxyPort = 3128;

        IWebProxy? originalProxy;
        string originalProxyHost;
        string originalProxyPort;

        StringWriter errorStream;
        TextWriter originalConsoleErrorOut;

        [SetUp]
        public void SetUp()
        {
            SetLocalEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
            SetCiEnvironmentProxySettings(NonExistentProxyHostname, NonExistentProxyPort);
            SetConsoleErrorOut();
        }

        /// <summary>
        /// Configuring all the infrastructure required for a proper proxy test (with blocking certain addresses, proxy
        /// server itself etc) is over the top for a test here. We can implicitly test that the proxy settings are being
        /// picked up properly by setting a non-existent property, and ensuring that we fail with connectivity errors
        /// *to the non-existent proxy* rather than a successful healthcheck directly to Azure.
        /// </summary>
        [Test]
        public async Task ConfiguredProxy_IsUsedForHealthCheck()
        {
            // Arrange
            var randomName = SdkContext.RandomResourceName(nameof(AzureWebAppHealthCheckActionHandlerFixtures), 60);
            var clientId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionClientId);
            var clientSecret = ExternalVariables.Get(ExternalVariable.AzureSubscriptionPassword);
            var tenantId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionTenantId);
            var subscriptionId = ExternalVariables.Get(ExternalVariable.AzureSubscriptionId);

            // Act
            var result = await CommandTestBuilder.CreateAsync<HealthCheckCommand, Program>()
                                                 .WithArrange(context =>
                                                              {
                                                                  context.Variables.Add(AccountVariables.SubscriptionId, subscriptionId);
                                                                  context.Variables.Add(AccountVariables.TenantId, tenantId);
                                                                  context.Variables.Add(AccountVariables.ClientId, clientId);
                                                                  context.Variables.Add(AccountVariables.Password, clientSecret);
                                                                  context.Variables.Add(SpecialVariables.Action.Azure.ResourceGroupName, randomName);
                                                                  context.Variables.Add(SpecialVariables.Action.Azure.WebAppName, randomName);
                                                                  context.Variables.Add("Octopus.Account.AccountType", "AzureServicePrincipal");
                                                              })
                                                 .Execute(false);

            // Assert
            result.WasSuccessful.Should().BeFalse();

            // This also operates differently locally vs on CI, so combine both StdErr and Calamari Log to get
            // the full picture
            var windowsNetFxDnsError = "The remote name could not be resolved: 'non-existent-proxy.local'";
            var ubuntuDnsError = "Resource temporarily unavailable (non-existent-proxy.local:3128)";
            var generalLinuxDnsError = "Name or service not known (non-existent-proxy.local:3128)";
            var windowsDotNetDnsError = "No such host is known. (non-existent-proxy.local:3128)";

            errorStream.Flush();
            var calamariOutput = result.FullLog + errorStream;
            calamariOutput.Should().ContainAny(windowsDotNetDnsError, ubuntuDnsError, generalLinuxDnsError, windowsNetFxDnsError);
        }

        [TearDown]
        public void TearDown()
        {
            RestoreLocalEnvironmentProxySettings();
            RestoreCiEnvironmentProxySettings();
            RestoreConsoleErrorOut();
        }

        void SetConsoleErrorOut()
        {
            originalConsoleErrorOut = Console.Error;
            errorStream = new StringWriter();
            Console.SetError(errorStream);
        }

        void SetLocalEnvironmentProxySettings(string hostname, int port)
        {
            originalProxy = WebRequest.DefaultWebProxy;

            var proxySettings = new UseCustomProxySettings(hostname, port, null!, null!).CreateProxy().Value;
            WebRequest.DefaultWebProxy = proxySettings;
        }

        void SetCiEnvironmentProxySettings(string hostname, int port)
        {
            originalProxyHost = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost);
            originalProxyPort = Environment.GetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort);

            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, hostname);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, $"{port}");
        }

        void RestoreConsoleErrorOut()
        {
            Console.SetError(originalConsoleErrorOut);
        }

        void RestoreLocalEnvironmentProxySettings()
        {
            WebRequest.DefaultWebProxy = originalProxy;
        }

        void RestoreCiEnvironmentProxySettings()
        {
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyHost, originalProxyHost);
            Environment.SetEnvironmentVariable(EnvironmentVariables.TentacleProxyPort, originalProxyPort);
        }
    }
}