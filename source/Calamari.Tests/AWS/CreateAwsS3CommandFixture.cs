using System;
using System.Collections.Generic;
using Calamari.Aws.Commands;
using Calamari.Aws.Deployment;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Variables;
using Calamari.Testing.Helpers;
using FluentAssertions;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.AWS
{
    [TestFixture]
    [Category(TestCategory.RunOnceOnWindowsAndLinux)]
    public class CreateAwsS3CommandFixture
    {
        [Test]
        public void NonUniqueTags_ShouldThrow()
        {
            var tags = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("key1", "value1"),
                new KeyValuePair<string, string>("key1", "value2")
            };
            var variables = new CalamariVariables();
            variables.Set(AwsSpecialVariables.CloudFormation.Tags, JsonConvert.SerializeObject(tags));
            
            var command = new CreateAwsS3Command(new InMemoryLog(), variables);
            Action act = () => command.Execute(new[]
            {
                "--bucket", "test-bucket",
            });

            act.Should().Throw<CommandException>().WithMessage("*Each tag key must be unique.");
        }
    }
}