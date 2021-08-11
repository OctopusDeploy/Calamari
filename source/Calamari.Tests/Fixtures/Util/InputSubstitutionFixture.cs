using Calamari.Util;
using NUnit.Framework;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;

namespace Calamari.Tests.Fixtures.Util
{
    [TestFixture]
    public class InputSubstitutionFixture
    {
        [Test]
        [TestCase("#{Octopus.Action.Package[package].ExtractedPath}", "Octopus.Action.Package[package].ExtractedPath=C:\\OctopusTest\\Api Test\\1\\Octopus-Primary\\Work\\20210804020317-7-11\\package",
                     "C:\\\\OctopusTest\\\\Api Test\\\\1\\\\Octopus-Primary\\\\Work\\\\20210804020317-7-11\\\\package")]
        [TestCase("#{MyPassword[#{UserName}]}", "MyPassword[username]=C:\\OctopusTest;UserName=username",
                     "C:\\\\OctopusTest")]
        [TestCase("#{if BoolVariable}#{TestVariable}#{/if}", "BoolVariable=True;TestVariable=C:\\OctopusTest",
                     "C:\\\\OctopusTest")]
        [TestCase("#{if BoolVariable}#{TestVariable}#{else}#{OtherVariable}#{/if}", "BoolVariable=False;TestVariable=C:\\OctopusTest;OtherVariable=D:\\TestFolder",
                     "D:\\\\TestFolder")]
        [TestCase("#{if Octopus.Environment.Name == \"Production\"}#{TestVariable}#{/if}", "Octopus.Environment.Name=Production;TestVariable=C:\\OctopusTest",
                     "C:\\\\OctopusTest")]
        [TestCase("#{each w in MyWidgets}'#{w.Value.WidgetId}': #{if w.Value.WidgetId == WidgetIdSelector}This is my Widget!#{else}No widget matched :(#{/if}#{/each}",
                     "WidgetIdSelector=Widget-2;MyWidgets={\"One\":{\"WidgetId\":\"Widget-1\",\"Name\":\"Widget-One\"},\"Two\":{\"WidgetId\":\"Widget-2\",\"Name\":\"Widget-Two\"}}",
                     @"'Widget-1': No widget matched :('Widget-2': This is my Widget!")]
        [TestCase("#{each endpoint in Endpoints}#{endpoint},#{/each}", 
                     "Endpoints=C:\\A,D:\\B",
                     "C:\\\\A,D:\\\\B,")]
        [TestCase("#{FilterVariable | ToUpper}", "FilterVariable=C:\\somelowercase",
                     "C:\\\\SOMELOWERCASE")]
        [TestCase("#{MyVar | Match \"a b\"}", "MyVar=a b c",
                     "true")]
        [TestCase("#{MyVar | StartsWith \"Ab\"}", "MyVar=Abc",
                     "true")]
        [TestCase("#{MyVar | Match #{pattern}}", "MyVar=a b c;pattern=a b",
                     "true")]
        [TestCase("#{MyVar | Substring \"8\" \"6\"}", "MyVar=Octopus Deploy",
                     "Deploy")]
        public void CommonExpressionsAreEvaluated(string expression, string variables, string expectedResult)
        {
            var variableDictionary = ParseVariables(variables);
            string jsonInputs = "{\"testValue\":\"" + expression + "\"}";
            var log = Substitute.For<ILog>();
            string evaluatedInputs = InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variableDictionary, log);

            string expectedEvaluatedInputs = "{\"testValue\":\"" + expectedResult + "\"}";
            Assert.AreEqual(expectedEvaluatedInputs, evaluatedInputs);
            log.DidNotReceive().Warn(Arg.Any<string>());
        }
        
        [Test]
        public void SimpleExpressionsAreEvaluated()
        {
            var variableDictionary = new CalamariVariables();
            string jsonInputs = "{\"testValue\":\"#{ | NowDateUtc}\"}";
            var log = Substitute.For<ILog>();
            string evaluatedInputs = InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variableDictionary, log);

            evaluatedInputs.Should().NotBeEmpty();
            evaluatedInputs.Should().NotBeEquivalentTo(jsonInputs);
            
            log.DidNotReceive().Warn(Arg.Any<string>());
        }
        
        [Test]
        public void VariablesInJsonInputsShouldBeEvaluated()
        {
            var variables = new CalamariVariables
            {
                { "Octopus.Action.Package[package].ExtractedPath", "C:\\OctopusTest\\Api Test\\1\\Octopus-Primary\\Work\\20210804020317-7-11\\package" },
            };
            string jsonInputs = "{\"containerNameOverride\":\"payload\",\"package\":{\"extractedToPath\":\"#{Octopus.Action.Package[package].ExtractedPath}\"},\"target\":{\"files\":[{\"path\":\"azure-blob-container-target.0.0.0.zip\",\"fileName\":{\"type\":\"original file name\"}}]}}";
            string evaluatedInputs = InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variables, Substitute.For<ILog>());

            string expectedEvaluatedInputs = "{\"containerNameOverride\":\"payload\",\"package\":{\"extractedToPath\":\"C:\\\\OctopusTest\\\\Api Test\\\\1\\\\Octopus-Primary\\\\Work\\\\20210804020317-7-11\\\\package\"},\"target\":{\"files\":[{\"path\":\"azure-blob-container-target.0.0.0.zip\",\"fileName\":{\"type\":\"original file name\"}}]}}";
            Assert.AreEqual(expectedEvaluatedInputs, evaluatedInputs);
        }
            
        [Test]
        public void MissingVariableValue_LogsAWarning()
        {
            var variables = new CalamariVariables
            {
            };
            string jsonInputs = "{\"containerNameOverride\":\"payload\",\"package\":{\"extractedToPath\":\"#{Octopus.Action.Package[package].ExtractedPath}\"},\"target\":{\"files\":[]}}";
            var log = Substitute.For<ILog>();
            InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variables, log);
            log.Received().Warn(Arg.Any<string>());
        }
        
        [Test]
        public void InvalidVariableExpressionFails()
        {
            var variables = new CalamariVariables
            {
            };
            string jsonInputs = "{\"package\":{\"extractedToPath\":\"#{Octopus.Action.Package[package]...ExtractedPath}\"}}";
            Assert.Throws<CommandException>(() => InputSubstitution.SubstituteAndEscapeAllVariablesInJson(jsonInputs, variables, Substitute.For<ILog>()));
        }

        CalamariVariables ParseVariables(string variableDefinitions)
        {
            var variables = new CalamariVariables();

            var items = variableDefinitions.Split(';');
            foreach (var item in items)
            {
                var pair = item.Split('=');
                var key = pair.First();
                var value = pair.Last();
                variables[key] = value;
            }

            return variables;
        }
    }
}