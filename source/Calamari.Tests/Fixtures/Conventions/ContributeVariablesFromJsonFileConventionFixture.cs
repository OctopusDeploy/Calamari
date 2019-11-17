using System;
using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Processes;
using FluentAssertions;
using NUnit.Framework;
using Octostache;

namespace Calamari.Tests.Fixtures.Conventions
{
    [TestFixture]
    public class ContributeVariablesFromJsonFileConventionFixture
    {
        void RunTest(
            IEnumerable<(string Key, string Value)> existingVariables,
            IEnumerable<(string Key, string Value)> newVariables,
            Action<CalamariVariableDictionary> assertions
        )
        {
            var deploymentVariables = new CalamariVariableDictionary();

            foreach (var variable in existingVariables)
            {
                deploymentVariables.Add(variable.Key, variable.Value);
            }
            
            var deployment = new RunningDeployment(null, deploymentVariables);
            
            var fileVariables = new VariableDictionary();

            foreach (var variable in newVariables)
            {
                fileVariables.Add(variable.Key, variable.Value);
            }
            
            var convention = new ContributeVariablesFromJsonFileConvention(path => fileVariables);
            
            convention.Install(deployment);

            var actual = deployment.Variables;

            assertions(actual);
        }

        [Test]
        public void VariablesAreNotContributedIfEnvVarIsNotFound()
        {
            RunTest(
                new (string, string)[0],
                new[]
                {
                    ("new.key", "new.value")
                },
                actual => actual.Should().BeEmpty()
            );
        }

        [Test]
        public void VariablesAreContributedIfEnvVarIsFound()
        {
            RunTest(
                new[]
                {
                    (ContributeVariablesFromJsonFileConvention.AdditionalVariablesKey, "ignored_by_this_test"),
                    ("existing.key", "existing.value")
                },
                new[]
                {
                    ("new.key", "new.value")
                },
                actual =>
                {
                    actual.Should().HaveCount(3);
                    actual.Should().ContainSingle(pair => 
                        pair.Key == ContributeVariablesFromJsonFileConvention.AdditionalVariablesKey &&
                        pair.Value == "ignored_by_this_test");
                    actual.Should().ContainSingle(pair => 
                        pair.Key == "existing.key" &&
                        pair.Value == "existing.value");
                    actual.Should().ContainSingle(pair => 
                        pair.Key == "new.key" &&
                        pair.Value == "new.value");
                }
            );
        }

        [Test]
        public void ThrowsExceptionIfFileNotFound()
        {
            const string filePath = "c:/assuming/that/this/file/doesnt/exist.json";
            
            var deployment = new RunningDeployment(
                null,
                new CalamariVariableDictionary
                {
                    { ContributeVariablesFromJsonFileConvention.AdditionalVariablesKey, filePath }
                }
            );

            var convention = new ContributeVariablesFromJsonFileConvention();

            convention
                .Invoking(c => c.Install(deployment))
                .Should()
                .Throw<CommandException>()
                // Make sure that the message says how to turn this feature off.
                .Where(e => e.Message.Contains(ContributeVariablesFromJsonFileConvention.AdditionalVariablesKey))
                // Make sure that the message says where it looked for the file.
                .Where(e => e.Message.Contains(filePath));
        }
    }
}