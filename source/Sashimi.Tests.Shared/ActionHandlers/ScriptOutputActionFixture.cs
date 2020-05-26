using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.Tests.Shared.ActionHandlers
{
    public class ScriptOutputActionFixture
    {
        [Test]
        public void GetStrings_ShouldReturnSingleValue()
        {
            ScriptOutputAction action = new ScriptOutputAction("name", new Dictionary<string, string>
            {
                {"property", "value"}
            });

            action.GetStrings("property").Should().Equal("value");
        }

        [Test]
        public void GetStrings_ShouldReturnMultipleValues()
        {
            ScriptOutputAction action = new ScriptOutputAction("name", new Dictionary<string, string>
            {
                {"property", "value1, value2"}
            });

            var values = action.GetStrings("property");
            values.Should().Contain("value1");
            values.Should().Contain("value2");
        }

        [Test]
        public void GetStrings_MultipleProperties_ShouldReturnMultipleValues()
        {
            ScriptOutputAction action = new ScriptOutputAction("name", new Dictionary<string, string>
            {
                {"property1", "value1"},
                {"property2", "value2"}
            });

            var values = action.GetStrings("property1", "property2");
            values.Should().Contain("value1");
            values.Should().Contain("value2");
        }

        [Test]
        public void GetStrings_ShouldReturnNoValues()
        {
            ScriptOutputAction action = new ScriptOutputAction("name", new Dictionary<string, string>
            {
                {"property", "value"},
            });

            action.GetStrings("non-existant-property").Should().BeNullOrEmpty();
        }

    }
}