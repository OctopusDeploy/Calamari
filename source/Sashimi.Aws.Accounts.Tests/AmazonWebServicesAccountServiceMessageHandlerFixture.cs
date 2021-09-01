using System;
using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ServiceMessages;

namespace Sashimi.Aws.Accounts.Tests
{
    [TestFixture]
    public class AmazonWebServicesAccountServiceMessageHandlerFixture
    {
        ICreateAccountDetailsServiceMessageHandler serviceMessageHandler;

        [OneTimeSetUp]
        public void SetUp()
        {
            serviceMessageHandler = new AmazonWebServicesAccountServiceMessageHandler();
        }

        [Test]
        public void Ctor_Properties_ShouldBeInitializedCorrectly()
        {
            serviceMessageHandler.AuditEntryDescription.Should().Be("AWS Account");
            serviceMessageHandler.ServiceMessageName.Should().Be(AmazonWebServicesAccountServiceMessageHandler.CreateAwsAccountServiceMessagePropertyNames.CreateAccountName);
        }

        [Test]
        public void CreateAccountDetails_ShouldCreateDetailsCorrectly()
        {
            var properties = GetMessageProperties();

            var details = serviceMessageHandler.CreateAccountDetails(properties, Substitute.For<ITaskLog>());

            details.Should().BeOfType<AmazonWebServicesAccountDetails>();
            var amazonWebServicesAccountDetails = (AmazonWebServicesAccountDetails)details;
            amazonWebServicesAccountDetails.AccessKey.Should().Be(properties[AmazonWebServicesAccountServiceMessageHandler.CreateAwsAccountServiceMessagePropertyNames.AccessKeyAttribute]);
            amazonWebServicesAccountDetails.SecretKey.Should().Be(properties[AmazonWebServicesAccountServiceMessageHandler.CreateAwsAccountServiceMessagePropertyNames.SecretKeyAttribute]);
        }

        static IDictionary<string, string> GetMessageProperties()
        {
            return new Dictionary<string, string>
            {
                { AmazonWebServicesAccountServiceMessageHandler.CreateAwsAccountServiceMessagePropertyNames.AccessKeyAttribute, "AccessKey" },
                { AmazonWebServicesAccountServiceMessageHandler.CreateAwsAccountServiceMessagePropertyNames.SecretKeyAttribute, "SecretKey" }
            };
        }
    }
}