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
        private void RunTest(
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
            RunTest(
                canParseAsJson: true,
                canParseAsYaml: false,
                invocation => invocation
                    .Should()
                    .NotThrow()
            );
        }
        
        [Test]
        public void ShouldNotThrowIfTheFileCanBeParsedAsYaml()
        {
            RunTest(
                canParseAsJson: false,
                canParseAsYaml: true,
                invocation => invocation
                    .Should()
                    .NotThrow()
            );
        }
        
        [Test]
        public void ShouldThrowIfTheFileCantBeParsedWithAllReplacers()
        {
            RunTest(
                canParseAsJson: false,
                canParseAsYaml: false,
                invocation => invocation
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