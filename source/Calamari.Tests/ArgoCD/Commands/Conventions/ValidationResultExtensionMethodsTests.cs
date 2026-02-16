using System;
using Calamari.ArgoCD.Conventions;
using Calamari.Common.Commands;
using Calamari.Testing.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class ValidationResultExtensionMethodsTests
    {
        [Test]
        public void Success_NoWarningsOrException()
        {
            InMemoryLog log = new InMemoryLog();
            ValidationResult result = ValidationResult.Success;
            
            result.Action(log);
            
            log.Messages.Should().BeEmpty();
        }
        
        [Test]
        public void HasWarning_WarningsLogged()
        {
            InMemoryLog log = new InMemoryLog();
            ValidationResult result = ValidationResult.Warning("msg 1", "msg 2");
            
            result.Action(log);

            log.Messages.Should()
               .SatisfyRespectively(m =>
                        {
                            m.Level.Should().Be(InMemoryLog.Level.Warn);
                            m.FormattedMessage.Should().Be("msg 1");
                        },
                        m =>
                        {
                            m.Level.Should().Be(InMemoryLog.Level.Warn);
                            m.FormattedMessage.Should().Be("msg 2");
                        });
        }

        [Test]
        public void Error_ThrowsException()
        {
            InMemoryLog log = new InMemoryLog();
            ValidationResult result = ValidationResult.Error("error 1", "error 2");
            
            Action action = () => result.Action(log);
            action.Should().Throw<CommandException>()
                  .WithMessage("error 1", "Only throw the first error");
        }
    }
}
