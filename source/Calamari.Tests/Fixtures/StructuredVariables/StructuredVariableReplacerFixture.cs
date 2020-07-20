using System;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;
using Calamari.Tests.Helpers;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.Fixtures.StructuredVariables
{
    public class StructuredVariableReplacerFixture
    {
        void RunTest(
            bool canParseAsJson,
            bool canParseAsYaml,
            Action<Action> invocationAssertions
        )
        {
            var jsonReplacer = Substitute.For<IJsonFormatVariableReplacer>();
            var yamlReplacer = Substitute.For<IYamlFormatVariableReplacer>();

            jsonReplacer.TryModifyFile(Arg.Any<string>(), Arg.Any<IVariables>()).Returns(canParseAsJson);
            yamlReplacer.TryModifyFile(Arg.Any<string>(), Arg.Any<IVariables>()).Returns(canParseAsYaml);

            var replacer = new StructuredConfigVariableReplacer(jsonReplacer, yamlReplacer, new InMemoryLog());
            var variables = new CalamariVariables
            {
                { StructuredConfigVariableReplacer.FeatureToggleVariableName, "true" }
            };

            invocationAssertions(replacer.Invoking(r => r.ModifyFile("path", variables)));
        }

        [Test]
        public void ShouldNotThrowIfTheFileCanBeParsedAsJson()
        {
            RunTest(
                    true,
                    false,
                    invocation => invocation
                                  .Should()
                                  .NotThrow()
                   );
        }

        [Test]
        public void ShouldNotThrowIfTheFileCanBeParsedAsYaml()
        {
            RunTest(
                    false,
                    true,
                    invocation => invocation
                                  .Should()
                                  .NotThrow()
                   );
        }

        [Test]
        public void ShouldThrowIfTheFileCantBeParsedWithAllReplacers()
        {
            RunTest(
                    false,
                    false,
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

            var replacer = new StructuredConfigVariableReplacer(jsonReplacer, yamlReplacer, new InMemoryLog());
            var variables = new CalamariVariables
            {
                { StructuredConfigVariableReplacer.FeatureToggleVariableName, "true" }
            };

            replacer.ModifyFile("path", variables);

            yamlReplacer.DidNotReceiveWithAnyArgs().TryModifyFile("", null);
        }
    }
}