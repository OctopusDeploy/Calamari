using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Plumbing.Logging;
using Calamari.Tests.KubernetesFixtures;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Logger
{
    [TestFixture]
    public class AbstractLogFixture
    {
        const string Password = "mySuperSecretPassword";

        [Test]
        public void AddValueToRedact_ReplacesRedactedStringWithGivenPlaceholder()
        {
            const string placeholder = "<placeholder-for-secrets>";
            Func<string,string> logMessage = s => $"here is my super cool message with {s} but is it redacted?";
            var log = new TestSerializedLog();
            log.AddValueToRedact(Password, placeholder);
            log.Info(logMessage(Password));

            // Skip 1 because the first log is setting the log level ('##octopus[stdout-default]')
            log.AllOutput.Skip(1).Should().ContainSingle().Which.Should().Be(logMessage(placeholder));
        }

        [Test]
        public void AddValueToRedact_RedactsAllInstancesOfGivenString_WhenAddedBeforeLogging()
        {
            const string logMessage = "here is a message with " + Password + " and other text";
            const string logFormatString = "here is a log format string {0} where  stuff is added in the middle";
            var log = new TestSerializedLog();
            log.AddValueToRedact(Password, "<password>");
            log.Error(logMessage);
            log.ErrorFormat(logFormatString, Password);
            log.Warn(logMessage);
            log.WarnFormat(logFormatString, Password);
            log.Info(logMessage);
            log.InfoFormat(logFormatString, Password);
            log.Verbose(logMessage);
            log.VerboseFormat(logFormatString, Password);

            log.AllOutput.Should().NotContain(m => m.Contains(Password));
        }

        [Test]
        public void AddValueToRedact_ValueReplacementsCanBeUpdated()
        {
            var log = new DoNotDoubleLog();
            log.AddValueToRedact(Password, "<my-placeholder>");

            Action act = () => log.AddValueToRedact(Password, "<password>");

            act.Should().NotThrow(because: "you can update the placeholder for a given redacted value.");
        }

        public class TestSerializedLog : AbstractLog
        {
            public List<string> AllOutput { get; } = new List<string>();
            protected override void StdOut(string message)
            {
                AllOutput.Add(message);
            }

            protected override void StdErr(string message)
            {
                AllOutput.Add(message);
            }
        }
    }
}