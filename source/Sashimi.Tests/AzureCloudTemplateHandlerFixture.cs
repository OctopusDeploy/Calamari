using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Sashimi.Server.Contracts;

namespace Sashimi.AzureAppService.Tests
{
    public class AzureCloudTemplateHandlerFixture
    {
        IFormatIdentifier formatIdentifierTrue;
        IFormatIdentifier formatIdentifierFalse;

        [SetUp]
        public void SetUp()
        {
            formatIdentifierTrue = Substitute.For<IFormatIdentifier>();
            formatIdentifierTrue.IsJson(Arg.Any<string>()).ReturnsForAnyArgs(true);

            formatIdentifierFalse = Substitute.For<IFormatIdentifier>();
            formatIdentifierFalse.IsJson(Arg.Any<string>()).ReturnsForAnyArgs(false);
        }

        [Test]
        public void RespondsToCorrectTemplateAndProvider()
        {
            new AzureCloudTemplateHandler(formatIdentifierTrue).CanHandleTemplate("AzureAppService", "{\"hi\": \"there\"}").Should().BeTrue();
            new AzureCloudTemplateHandler(formatIdentifierTrue).CanHandleTemplate("AzureAppService", "#{blah}").Should().BeTrue();
            new AzureCloudTemplateHandler(formatIdentifierFalse).CanHandleTemplate("AzureAppService", "#{blah}").Should().BeTrue();
        }
    }
}