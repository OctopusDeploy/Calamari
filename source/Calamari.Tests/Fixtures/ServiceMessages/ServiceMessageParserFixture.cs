using System;
using System.Collections.Generic;
using Calamari.Integration.ServiceMessages;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ServiceMessages
{
    [TestFixture]
    public class ServiceMessageParserFixture
    {
        ServiceMessageParser parser;
        List<ServiceMessage> messages;

        [SetUp]
        public void SetUp()
        {
           messages = new List<ServiceMessage>();
           parser = new ServiceMessageParser((message) => messages.Add(message)); 
        }

        [Test]
        public void ShouldLeaveNonMessageText()
        {
            parser.Parse("Hello World!");
            Assert.IsEmpty(messages);
        }

        [Test]
        public void ShouldRecognizeBasicMessage()
        {
            parser.Parse("Hello world!" + Environment.NewLine);
            parser.Parse("##octopus[foo] Hello" + Environment.NewLine);

            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0].Name, Is.EqualTo("foo"));
        }

        [Test]
        public void ShouldRecognizeMultipleMessages()
        {
            parser.Parse("##octopus[Burt] Hello" + Environment.NewLine);
            parser.Parse("Hello world!" + Environment.NewLine);
            parser.Parse("##octopus[Ernie] Hello" + Environment.NewLine);

            Assert.That(messages.Count, Is.EqualTo(2));
            Assert.That(messages[0].Name, Is.EqualTo("Burt"));
            Assert.That(messages[1].Name, Is.EqualTo("Ernie"));
        }

        [Test]
        public void ShouldRecognizeMessageOverMultipleLines()
        {
            parser.Parse("Hello world!" + Environment.NewLine);
            parser.Parse("##octopus[foo name='UGF1bA==' value='dGhpcyBpcyBhIHZlcnkNCg" + Environment.NewLine + Environment.NewLine);
            parser.Parse("0KdmVyeSBsb25nIGxvbmcgbGluZQ=='] Hello" + Environment.NewLine);

            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0].Name, Is.EqualTo("foo"));
            Assert.That(messages[0].Properties["name"], Is.EqualTo("Paul"));
            Assert.That(messages[0].Properties["value"], Is.EqualTo("this is a very" + Environment.NewLine + Environment.NewLine + "very long long line"));
        }

    }
}