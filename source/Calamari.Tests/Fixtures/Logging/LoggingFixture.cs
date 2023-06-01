using Calamari.Common.Plumbing.ServiceMessages;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Tests.Fixtures.Logging
{
    [TestFixture]
    public class LoggingFixture
    {
        [Test]
        public void WriteServiceMessage_CorrectlyFormatsMessages()
        {
            // Arrange
            var serviceMessage = ServiceMessage.Create("do-it", new KeyValuePair<string, string>("foo", "1"), new KeyValuePair<string, string>("bar", null));

            // Testing functionality from abstract base class, so it doesn't matter that this is a test class.
            var sut = new InMemoryLog();

            // Act
            sut.WriteServiceMessage(serviceMessage);

            // Assert
            sut.Messages.Should().ContainSingle().Which.FormattedMessage.Should().Be("##octopus[do-it foo=\"MQ==\"]");
        }
    }
}
