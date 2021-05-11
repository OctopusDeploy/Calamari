using System.Collections.Generic;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts.ServiceMessages;
using CreateAwsAccountServiceMessagePropertyNames = Sashimi.Aws.Accounts.AmazonWebServicesAccountServiceMessageHandler.CreateAwsAccountServiceMessagePropertyNames;

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
            serviceMessageHandler.ServiceMessageName.Should().Be(CreateAwsAccountServiceMessagePropertyNames.CreateAccountName);
        }

        [Test]
        public void CreateAccountDetails_ShouldCreateDetailsCorrectly()
        {
            var properties = GetMessageProperties();

            var details = serviceMessageHandler.CreateAccountDetails(properties, Substitute.For<ITaskLog>());

            details.Should().BeOfType<AmazonWebServicesAccountDetails>();
            var amazonWebServicesAccountDetails = (AmazonWebServicesAccountDetails)details;
            amazonWebServicesAccountDetails.AccessKey.Should().Be(properties[CreateAwsAccountServiceMessagePropertyNames.AccessKeyAttribute]);
            amazonWebServicesAccountDetails.SecretKey.Should().Be(properties[CreateAwsAccountServiceMessagePropertyNames.SecretKeyAttribute]);
        }

        static IDictionary<string, string> GetMessageProperties()
        {
            return new Dictionary<string, string>
            {
                { CreateAwsAccountServiceMessagePropertyNames.AccessKeyAttribute, "AccessKey" },
                { CreateAwsAccountServiceMessagePropertyNames.SecretKeyAttribute, "SecretKey" }
            };
        }
    }
}