#nullable disable
using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Diagnostics;
using Octostache;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.Endpoints;
using Sashimi.Server.Contracts.ServiceMessages;
using AzureCloudServiceServiceMessageNames =
    Sashimi.AzureCloudService.AzureCloudServiceServiceMessageHandler.AzureCloudServiceServiceMessageNames;
using AzureCloudServiceEndpointDeploymentSlot =
    Sashimi.AzureCloudService.AzureCloudServiceServiceMessageHandler.AzureCloudServiceEndpointDeploymentSlot;

namespace Sashimi.AzureCloudService.Tests
{
    [TestFixture]
    public class AzureCloudServiceServiceMessageHandlerFixture
    {
        ICreateTargetServiceMessageHandler serviceMessageHandler;
        ISystemLog logger;

        [SetUp]
        public void SetUp()
        {
            logger = Substitute.For<ISystemLog>();
            serviceMessageHandler = new AzureCloudServiceServiceMessageHandler(logger);
        }

        [Test]
        public void Ctor_Properties_ShouldBeInitializedProperly()
        {
            serviceMessageHandler.AuditEntryDescription.Should().Be("Azure Cloud Service Target");
            serviceMessageHandler.ServiceMessageName.Should().Be(AzureCloudServiceServiceMessageNames.CreateTargetName);
        }

        [Test]
        public void BuildEndpoint_WhenAbleToResolveAccountIdUsingAccountIdOrNameAttribute_ShouldNotTryToResolveUsingAccountIdInVariables()
        {
            var messageProperties = GetMessageProperties();
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-1";
            string ResolveAccountId(string key)
            {
                if (key == messageProperties[AzureCloudServiceServiceMessageNames.AccountIdOrNameAttribute])
                    return accountId;

                return null;
            }

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, ResolveAccountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
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
            messageProperties[AzureCloudServiceServiceMessageNames.AccountIdOrNameAttribute] = accountIdOrNameInMessageProperties;
            var variableDict = GetVariableDictionary();

            const string accountId = "Accounts-12";
            string ResolveAccountId(string key)
            {
                if (key == variableDict[SpecialVariables.Action.Azure.AccountId])
                    return accountId;

                return null;
            }

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, ResolveAccountId,
                                                               null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
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
                if (key == messageProperties[AzureCloudServiceServiceMessageNames.AccountIdOrNameAttribute])
                    return accountIdResolvedUsingAccountOrNameAttribute;
                if (key == variableDict[SpecialVariables.Action.Azure.AccountId])
                    return accountId;

                return null;
            }

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, ResolveAccountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
            });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        public void BuildEndpoint_WhenUnableToResolveAccountIdAtAll_ShouldThrowException(string accountId)
        {
            var messageProperties = GetMessageProperties();
            var variableDict = GetVariableDictionary();

            Action action = () => serviceMessageHandler.BuildEndpoint(messageProperties, variableDict, _ => accountId,
                                                               null, null, null, null);

            var expectedErrorMessage = $"Account with Id / Name, {variableDict.Get(SpecialVariables.Action.Azure.AccountId)}, not found.";
            action.Should().Throw<Exception>().Which.Message.Should().Be(expectedErrorMessage);
            logger.Received(1).Error(Arg.Is(expectedErrorMessage));
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("    ")]
        [TestCase("DoNotDeploy")]
        public void BuildEndpoint_WhenSwapPropertyIsNullOrWhiteSpaceOrNotDeploy_ShouldReturnEndpointWithSwapPropertyAsTrue(
                string swapValue)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureCloudServiceServiceMessageNames.SwapAttribute] = swapValue;
            const string accountId = "Accounts-2";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                GetVariableDictionary(), _ => accountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = true,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
            });
        }

        [Test]
        [TestCase("deploy")]
        [TestCase("depLoY")]
        public void BuildEndpoint_WhenSwapPropertyIsDeploy_ShouldReturnEndpointWithSwapPropertyAsFalse(string swapValue)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureCloudServiceServiceMessageNames.SwapAttribute] = swapValue;
            const string accountId = "Accounts-2";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                GetVariableDictionary(), _ => accountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
            });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("stage")]
        public void BuildEndpoint_WhenSlotPropertyIsNullOrEmptyOrNotProduction_ShouldReturnEndpointWithSlotPropertyAsStaging(
                string slotValue)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureCloudServiceServiceMessageNames.AzureDeploymentSlotAttribute] = slotValue;
            const string accountId = "Accounts-2";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                GetVariableDictionary(), _ => accountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Staging,
                UseCurrentInstanceCount = false
            });
        }

        [Test]
        [TestCase("production")]
        [TestCase("ProduCtion")]
        public void BuildEndpoint_WhenSlotPropertyIsProduction_ShouldReturnEndpointWithSlotPropertyAsProduction(
            string slotValue)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureCloudServiceServiceMessageNames.AzureDeploymentSlotAttribute] = slotValue;
            const string accountId = "Accounts-2";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                GetVariableDictionary(), _ => accountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
            });
        }

        [Test]
        [TestCase(null)]
        [TestCase("")]
        [TestCase("NotConfiguration")]
        public void BuildEndpoint_WhenInstanceCountPropertyIsNullOrEmptyOrNotConfiguration_ShouldReturnEndpointWithUseCurrentInstanceCountPropertyAsTrue(
                string instanceCountValue)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureCloudServiceServiceMessageNames.InstanceCountAttribute] = instanceCountValue;
            const string accountId = "Accounts-2";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                GetVariableDictionary(), _ => accountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = true
            });
        }

        [Test]
        [TestCase("configuration")]
        [TestCase("ConfigurAtion")]
        public void BuildEndpoint_WhenInstanceCountPropertyIsConfiguration_ShouldReturnEndpointWithUseCurrentInstanceCountPropertyAsFalse(
                string instanceCountValue)
        {
            var messageProperties = GetMessageProperties();
            messageProperties[AzureCloudServiceServiceMessageNames.InstanceCountAttribute] = instanceCountValue;
            const string accountId = "Accounts-2";

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                GetVariableDictionary(), _ => accountId,
                null, null, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false
            });
        }

        [Test]
        public void BuildEndpoint_WhenWorkerPoolProvided_ShouldUseWorkerPool()
        {
            var messageProperties = GetMessageProperties();
            messageProperties.Add(AzureCloudServiceServiceMessageNames.WorkerPoolIdOrNameAttribute, "Worker Pool 1");
            var accountId = "Accounts-2";
            var workerPoolId = "WorkerPools-234";
            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                                                               GetVariableDictionary(), _ => accountId,
                                                               null, _ => workerPoolId, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false,
                WorkerPoolId = workerPoolId
            });
        }

        [Test]
        public void BuildEndpoint_WhenNoWorkerPoolProvided_ShouldUseWorkerPoolFromStep()
        {
            var messageProperties = GetMessageProperties();
            messageProperties.Remove(AzureCloudServiceServiceMessageNames.WorkerPoolIdOrNameAttribute);
            var accountId = "Accounts-2";
            var workerPoolId = "WorkerPools-234";
            var variables = GetVariableDictionary();
            variables.Add(KnownVariables.WorkerPool.Id, workerPoolId);

            var endpoint = serviceMessageHandler.BuildEndpoint(messageProperties,
                                                               variables, _ => accountId,
                                                               null, _ => workerPoolId, null, null);

            AssertAzureCloudServiceEndpoint(endpoint, new ExpectedEndpointValues
            {
                AccountId = accountId,
                CloudServiceName = messageProperties[AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute],
                StorageAccountName = messageProperties[AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute],
                SwapIfPossible = false,
                Slot = AzureCloudServiceEndpointDeploymentSlot.Production,
                UseCurrentInstanceCount = false,
                WorkerPoolId = workerPoolId
            });
        }

        static void AssertAzureCloudServiceEndpoint(Endpoint actualEndpoint,
            ExpectedEndpointValues expectedEndpointValues)
        {
            actualEndpoint.Should().BeOfType<AzureCloudServiceEndpoint>();
            var cloudServiceEndpoint = (AzureCloudServiceEndpoint) actualEndpoint;
            cloudServiceEndpoint.AccountId.Should().Be(expectedEndpointValues.AccountId);
            cloudServiceEndpoint.CloudServiceName.Should().Be(expectedEndpointValues.CloudServiceName);
            cloudServiceEndpoint.StorageAccountName.Should().Be(expectedEndpointValues.StorageAccountName);
            cloudServiceEndpoint.Slot.Should().Be(expectedEndpointValues.Slot);
            cloudServiceEndpoint.SwapIfPossible.Should().Be(expectedEndpointValues.SwapIfPossible);
            cloudServiceEndpoint.UseCurrentInstanceCount.Should()
                .Be(expectedEndpointValues.UseCurrentInstanceCount);
            cloudServiceEndpoint.DefaultWorkerPoolId.Should().Be(expectedEndpointValues.WorkerPoolId);
        }

        static IDictionary<string, string> GetMessageProperties()
        {
            return new Dictionary<string, string>
            {
                {AzureCloudServiceServiceMessageNames.AccountIdOrNameAttribute, "Accounts-1"},
                {AzureCloudServiceServiceMessageNames.AzureCloudServiceNameAttribute, "CloudService"},
                {AzureCloudServiceServiceMessageNames.AzureStorageAccountAttribute, "AzureStorage"},
                {AzureCloudServiceServiceMessageNames.AzureDeploymentSlotAttribute, "production"},
                {AzureCloudServiceServiceMessageNames.InstanceCountAttribute, "configuration"},
                {AzureCloudServiceServiceMessageNames.SwapAttribute, "deploy"},
            };
        }

        static VariableDictionary GetVariableDictionary()
        {
            return new VariableDictionary {{SpecialVariables.Action.Azure.AccountId, "Accounts-2"}};
        }

        class ExpectedEndpointValues
        {
            public string AccountId { get; set; }
            public string CloudServiceName { get; set; }
            public string StorageAccountName { get; set; }
            public string Slot { get; set; }
            public bool SwapIfPossible { get; set; }
            public bool UseCurrentInstanceCount { get; set; }
            public string WorkerPoolId { get; set; }
        }
    }
}