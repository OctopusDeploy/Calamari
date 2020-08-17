using System;
using System.Collections.Generic;
using Calamari.Common.Plumbing.ServiceMessages;
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
        public void ShouldRecognizeMessageWithNewLineChar()
        {
            const string stringWithEncodedCrLf = "dGhpcyBpcyBhIHZlcnkNCg0KdmVyeSBsb25nIGxvbmcgbGluZQ==";
            parser.Parse("Hello world!" + Environment.NewLine);
            parser.Parse(string.Format("##octopus[foo name='UGF1bA==' value='{0}'] Hello", stringWithEncodedCrLf));

            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0].Name, Is.EqualTo("foo"));
            Assert.That(messages[0].Properties["name"], Is.EqualTo("Paul"));
            Assert.That(messages[0].Properties["value"], Is.EqualTo(string.Format("this is a very\r\n\r\nvery long long line")));
        }

        [Test]
        public void ShouldRecognizeMessageSplitOverMultipleLines()
        {
            parser.Parse("Hello world!" + Environment.NewLine);
            parser.Parse(string.Format("##octopus[foo name{0}='UGF1bA==' value='VGhpcyBzZ{0}W50ZW5jZSBpcyBmYWx{0}{0}zZQ=='] Hello", Environment.NewLine));

            Assert.That(messages.Count, Is.EqualTo(1));
            Assert.That(messages[0].Name, Is.EqualTo("foo"));
            Assert.That(messages[0].Properties["name"], Is.EqualTo("Paul"));
            Assert.That(messages[0].Properties["value"], Is.EqualTo(string.Format("This sentence is false")));
        }

    }
}