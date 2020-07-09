using System;
using Calamari.Features.StructuredVariables;
using Calamari.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    public class StructuredVariableReplacerFixture
    {
        private void RunExceptionTest(
            bool canParseAsJson,
            bool canParseAsYaml,
            Action<Action> invocationAssertions
        )
        {
            var jsonReplacer = Substitute.For<IJsonFormatVariableReplacer>();
            var yamlReplacer = Substitute.For<IYamlFormatVariableReplacer>();

            jsonReplacer.TryModifyFile(Arg.Any<string>(), Arg.Any<IVariables>()).Returns(canParseAsJson);
            yamlReplacer.TryModifyFile(Arg.Any<string>(), Arg.Any<IVariables>()).Returns(canParseAsYaml);
            
            var replacer = new StructuredConfigVariableReplacer(jsonReplacer, yamlReplacer);
            var variables = new CalamariVariables();

            invocationAssertions(replacer.Invoking(r => r.ModifyFile("path", variables)));
        }
        
        [Test]
        public void ShouldNotThrowIfTheFileCanBeParsedAsJson()
        {
            RunExceptionTest(
                canParseAsJson: true,
                canParseAsYaml: false,
                invocationResult => invocationResult
                    .Should()
                    .NotThrow()
            );
        }
        
        [Test]
        public void ShouldNotThrowIfTheFileCanBeParsedAsYaml()
        {
            RunExceptionTest(
                canParseAsJson: false,
                canParseAsYaml: true,
                invocationResult => invocationResult
                    .Should()
                    .NotThrow()
            );
        }
        
        [Test]
        public void ShouldThrowIfTheFileCantBeParsedWithAllReplacers()
        {
            RunExceptionTest(
                canParseAsJson: false,
                canParseAsYaml: false,
                invocationResult => invocationResult
                    .Should()
                    .ThrowExactly<Exception>()
                    .WithMessage("The config file at 'path' couldn't be parsed.")
            );
        }

        [Test]
        public void ShouldOnlyTryYamlIfFileIsNotJson()
        {
            var jsonReplacer = Substitute.For<IJsonFormatVariableReplacer>();
            var yamlReplacer = Substitute.For<IYamlFormatVariableReplacer>();

            jsonReplacer.TryModifyFile(Arg.Any<string>(), Arg.Any<IVariables>()).Returns(true);
            
            var replacer = new StructuredConfigVariableReplacer(jsonReplacer, yamlReplacer);
            var variables = new CalamariVariables();

            replacer.ModifyFile("path", variables);
            
            yamlReplacer.DidNotReceiveWithAnyArgs().TryModifyFile("", null);
        }
    }
}