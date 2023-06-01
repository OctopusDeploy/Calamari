using Calamari.Common.Plumbing.ServiceMessages;
using FluentAssertions;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calamari.Tests.Fixtures.ServiceMessages
{
    [TestFixture]
    public class ServiceMessageFixture
    {
        [Test]
        public void ToString_FormatsMessageCorrectly()
        {
            // Arrange
            var sut = ServiceMessage.Create("my-message-name", new KeyValuePair<string, string>("foo", "my-parameter-value"));

            // Act
            var text = sut.ToString();

            // Assert
            // Base65 encoding "my-parameter-value" gives "bXktcGFyYW1ldGVyLXZhbHVl"
            text.Should().Be("##octopus[my-message-name foo=\"bXktcGFyYW1ldGVyLXZhbHVl\"]");
        }

        [Test]
        public void ToString_IgnoresNullParameters()
        {
            // Arrange
            var sut = ServiceMessage.Create("my-message-name", new KeyValuePair<string, string>("foo", "1"), new KeyValuePair<string, string>("bar", null));

            // Act
            var text = sut.ToString();

            // Assert
            // Base64 encoding "1" gives "MQ=="
            text.Should().Be("##octopus[my-message-name foo=\"MQ==\"]");
        }
    }
}
