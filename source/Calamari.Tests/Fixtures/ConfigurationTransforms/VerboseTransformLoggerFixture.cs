using Calamari.Common.Features.ConfigurationTransforms;
using Calamari.Testing.Helpers;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.ConfigurationTransforms
{
    [TestFixture]
    public class VerboseTransformLoggerFixture
    {
        InMemoryLog log;

        [SetUp]
        public void SetUp()
        {
            log = new InMemoryLog();
        }

        [Test]
        [TestCase(TransformLoggingOptions.None, InMemoryLog.Level.Warn)]
        [TestCase(TransformLoggingOptions.LogWarningsAsErrors, InMemoryLog.Level.Error)]
        [TestCase(TransformLoggingOptions.LogWarningsAsInfo, InMemoryLog.Level.Info)]
        public void ShouldLogWarningsToConfiguredLevel(TransformLoggingOptions options, InMemoryLog.Level expectedLevel)
        {
            var target = new VerboseTransformLogger(options, log);

            const string message = "This is a warning";
            target.LogWarning(message);

            log.Messages.Should().Contain(m => m.Level == expectedLevel, message);
        }

        [Test]
        [TestCase(TransformLoggingOptions.None, InMemoryLog.Level.Error)]
        [TestCase(TransformLoggingOptions.LogExceptionsAsWarnings, InMemoryLog.Level.Warn)]
        public void ShouldLogErrorsToConfiguredLevel(TransformLoggingOptions options, InMemoryLog.Level expectedLevel)
        {
            var target = new VerboseTransformLogger(options, log);

            const string message = "This is an error";
            target.LogError(message);

            log.Messages.Should().Contain(m => m.Level == expectedLevel, message);
        }
    }
}