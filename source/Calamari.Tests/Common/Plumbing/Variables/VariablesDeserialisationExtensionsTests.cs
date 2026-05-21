using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using Calamari.Common.Commands;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Calamari.Tests.Common.Plumbing.Variables;

public class VariablesDeserialisationExtensionsTests
{
    const string TestKey = "TestKey";
    
    [Test]
    public void VariableWithEmptyString_Throws()
    {
        var variables = new CalamariVariables
        {
            { TestKey,  "" }
        };

        var action = () => variables.GetValueDeserialisedAs<TestVariableClass>(TestKey);

        action.Should().Throw<CommandException>();
    }
    
    [Test]
    public void NonexistentVariableName_Throws()
    {
        var variables = new CalamariVariables();

        var action = () => variables.GetValueDeserialisedAs<TestVariableClass>(TestKey);

        action.Should().Throw<CommandException>();
    }
    
    [Test]
    public void VariableWitNonJsonValue_Throws()
    {
        var variables = new CalamariVariables
        {
            { TestKey,  "No valid JSON to be found here" }
        };

        var action = () => variables.GetValueDeserialisedAs<TestVariableClass>(TestKey);

        action.Should().Throw<CommandException>();
    }

    [Test]
    public void VariableWithMismatchedValue_Throws()
    {
        var variables = new CalamariVariables
        {
            { TestKey,  """{ "isWalrus": true }""" }
        };

        var action = () => variables.GetValueDeserialisedAs<TestVariableClass>(TestKey);

        action.Should().Throw<CommandException>();
    }

    [Test]
    public void VariableWithMatchingJson_IsReturnsAsObject()
    {
        var variables = new CalamariVariables
        {
            { TestKey,  """{ "name": "Some Random Object", "numericValue": 18 }""" }
        };

        var result = variables.GetValueDeserialisedAs<TestVariableClass>(TestKey);

       result.Should().NotBeNull();
       result.Name.Should().Be("Some Random Object");
       result.NumericValue.Should().Be(18);
    }

    [Test]
    public void VaribleSeriliseedAndDeserialised_AppearsTheSame()
    {
        var inputObject = new TestVariableClass
        {
            Name = "Input Object",
            NumericValue = 42
        };
        var inputObjectString = JsonConvert.SerializeObject(inputObject, CalamariContractSerializationSettings.Default);
        var variables = new CalamariVariables
        {
            { TestKey,  inputObjectString }
        };
        
        var result = variables.GetValueDeserialisedAs<TestVariableClass>(TestKey);

        result.Should().BeEquivalentTo(inputObject);
    }
}

public class TestVariableClass
{
    public string Name { get; set; }
    public int NumericValue { get; set; }
}