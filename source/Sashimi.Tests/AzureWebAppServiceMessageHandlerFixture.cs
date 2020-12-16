#nullable disable
using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octostache;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;
using YamlDotNet.Core;
using AzureWebAppServiceMessageNames = Sashimi.AzureWebApp.AzureWebAppServiceMessageHandler.AzureWebAppServiceMessageNames;

namespace Sashimi.AzureWebApp.Tests
{
    public class AzureWebAppServiceMessageHandlerFixture
    {
        ICreateTargetServiceMessageHandler serviceMessageHandler;
        ILog logger;

        [SetUp]
        public void SetUp()
        {
            logger = Substitute.For<ILog>();
            serviceMessageHandler = new AzureWebAppServiceMessageHandler(logger);
        }

        [Test]
        public void Ctor_Properties_ShouldBeInitializedProperly()
        {
            serviceMessageHandler.AuditEntryDescription.Should().Be("Azure Web App Target");
            serviceMessageHandler.ServiceMessageName.Should().Be(AzureWebAppServiceMessageNames.CreateTargetName);
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void BuildEndpoint_WhenUnableToResolveAccountIdAtAll_ShouldThrowException(string accountId)
        {
            var messageProperties = GetMessageProperties();
            var variableDict = GetVariableDictionary();

            Action action = () => serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, _ => accountId, null, null, null);

            var expectedErrorMessage = $"Account with Id / Name, {variableDict.Get(SpecialVariables.Action.Azure.AccountId)}, not found.";
            action.Should().Throw<Exception>().Which.Message.Should().Be(expectedErrorMessage);
            logger.Received(1).Error(Arg.Is(expectedErrorMessage));
        }

        [Test]
        public void BuildEndpoint_WhenAbleToResolveAccountIdUsingAccountIdOrNameAttribute_ShouldNotTryToResolveUsingAccountIdInVariables()
        {
            var messageProperties = GetMessageProperties();
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-1";
            string ResolveAccountId(string key)
            {
                if (key == messageProperties[AzureWebAppServiceMessageNames.AccountIdOrNameAttribute])
                    return accountId;
                if (key == variableDict.Get(SpecialVariables.Action.Azure.AccountId))
                    return "123";
                return null;
            }

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, ResolveAccountId,
                null, null, null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = messageProperties[AzureWebAppServiceMessageNames.WebAppSlotNameAttribute]
            });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void BuildEndpoint_WhenAccountIdIsNotValidInMessageProperties_ShouldTryToResolveUsingAccountIdInVariables(
            string accountIdOrNameInMessageProperties)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureWebAppServiceMessageNames.AccountIdOrNameAttribute] = accountIdOrNameInMessageProperties;
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-12";
            string ResolveAccountId(string key)
            {
                if (key == variableDict[SpecialVariables.Action.Azure.AccountId])
                    return accountId;

                return null;
            }

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, ResolveAccountId,
                null, null, null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = messageProperties[AzureWebAppServiceMessageNames.WebAppSlotNameAttribute]
            });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void BuildEndpoint_WhenUnableToResolveAccountIdUsingAccountIdOrNameAttribute_ShouldTryToResolveUsingAccountIdInVariables(
            string accountIdResolvedUsingAccountOrNameAttribute)
        {
            var messageProperties = GetMessageProperties();
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-3";
            string ResolveAccountId(string key)
            {
                if (key == messageProperties[AzureWebAppServiceMessageNames.AccountIdOrNameAttribute])
                    return accountIdResolvedUsingAccountOrNameAttribute;
                if (key == variableDict[SpecialVariables.Action.Azure.AccountId])
                    return accountId;

                return null;
            }

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, ResolveAccountId,
                                                               null, null, null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = messageProperties[AzureWebAppServiceMessageNames.WebAppSlotNameAttribute]
            });
        }

        [Test]
        public void BuildEndpoint_WhenWebAppSlotNameAttributeIsMissing_ShouldReturnEndpointWithoutWebAppSlotName()
        {
            var messageProperties = GetMessageProperties();
            messageProperties.Remove(AzureWebAppServiceMessageNames.WebAppSlotNameAttribute);
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-12";
            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, _ => accountId,
                null, null, null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = string.Empty
            });
        }

        [Test]
        public void BuildEndpoint_WhenWebAppSlotNameAttributeIsNotMissing_ShouldReturnEndpointWithCorrectWebAppSlotName()
        {
            var messageProperties = GetMessageProperties();
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-12";
            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, _ => accountId,
                null, null, null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = messageProperties[AzureWebAppServiceMessageNames.WebAppSlotNameAttribute]
            });
        }

        [Test]
        public void BuildEndpoint_WhenWorkerPoolIdAttributeIsProvided_ShouldLookupWorkerPool()
        {
            var messageProperties = GetMessageProperties();
            messageProperties.Add(AzureWebAppServiceMessageNames.WorkerPoolIdOrNameAttribute, "Worker Pool 1");

            var workerPoolId = "WorkerPools-1";
            const string accountId = "Accounts-12";
            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                                                               GetVariableDictionary(),
                                                               _ => accountId,
                                                               null,
                                                               _ => workerPoolId,
                                                               null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = messageProperties[AzureWebAppServiceMessageNames.WebAppSlotNameAttribute],
                WorkerPoolId = workerPoolId
            });
        }

        [Test]
        public void BuildEndpoint_WhenNoWorkerPoolIdAttributeIsProvided_ShouldUseWorkerPoolFromStep()
        {
            var messageProperties = GetMessageProperties();
            messageProperties.Remove(AzureWebAppServiceMessageNames.WorkerPoolIdOrNameAttribute);
            const string accountId = "Accounts-12";
            var variableDictionary = GetVariableDictionary();
            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                                                               variableDictionary,
                                                               _ => accountId,
                                                               null,
                                                               _ => variableDictionary.Get(KnownVariables.WorkerPool.Id),
                                                               null);

            AssertAzureWebAppEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                WebAppName = messageProperties[AzureWebAppServiceMessageNames.WebAppNameAttribute],
                ResourceGroupName = messageProperties[AzureWebAppServiceMessageNames.ResourceGroupNameAttribute],
                WebAppSlotName = messageProperties[AzureWebAppServiceMessageNames.WebAppSlotNameAttribute],
                WorkerPoolId = variableDictionary.Get(KnownVariables.WorkerPool.Id)
            });
        }

        static void AssertAzureWebAppEndpoint(Endpoint actualEndpoint, ExpectedEndpointValues expectedEndpointValues)
        {
            actualEndpoint.Should().BeOfType<AzureWebAppEndpoint>();
            var azureWebAppEndpoint = (AzureWebAppEndpoint)actualEndpoint;
            azureWebAppEndpoint.AccountId.Should().Be(expectedEndpointValues.AccountId);
            azureWebAppEndpoint.ResourceGroupName.Should().Be(expectedEndpointValues.ResourceGroupName);
            azureWebAppEndpoint.WebAppName.Should().Be(expectedEndpointValues.WebAppName);
            azureWebAppEndpoint.WebAppSlotName.Should().Be(expectedEndpointValues.WebAppSlotName);
            azureWebAppEndpoint.DefaultWorkerPoolId.Should().Be(expectedEndpointValues.WorkerPoolId);
        }

        static IDictionary<string, string> GetMessageProperties()
        {
            return new Dictionary<string, string>
            {
                {AzureWebAppServiceMessageNames.AccountIdOrNameAttribute, "Accounts-1"},
                {AzureWebAppServiceMessageNames.WebAppNameAttribute, "CloudService"},
                {AzureWebAppServiceMessageNames.ResourceGroupNameAttribute, "AzureStorage"},
                {AzureWebAppServiceMessageNames.WebAppSlotNameAttribute, "production"},
            };
        }

        static VariableDictionary GetVariableDictionary()
        {
            return new VariableDictionary
            {
                { SpecialVariables.Action.Azure.AccountId, "Accounts-2" },
                { KnownVariables.WorkerPool.Id, "WorkerPools-100" },
            };
        }

        class ExpectedEndpointValues
        {
            public string AccountId { get; set; }
            public string WebAppName { get; set; }
            public string ResourceGroupName { get; set; }
            public string WebAppSlotName { get; set; }
            public string WorkerPoolId { get; set; } = string.Empty;
        }
    }
}