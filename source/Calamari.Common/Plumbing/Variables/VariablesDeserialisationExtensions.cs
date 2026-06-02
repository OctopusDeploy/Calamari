using System;
using Calamari.Common.Commands;
using Newtonsoft.Json;

namespace Calamari.Common.Plumbing.Variables;

public static class VariablesDeserialisationExtensions
{
    
    public static T GetValueDeserialisedAs<T>(this IVariables variables, string name)
    {
        var variableJson = variables.Get(name);

        if (string.IsNullOrEmpty(variableJson))
        {
            throw new CommandException($"Variable {name} was not supplied");
        }

        // Expand any `#{...}` references in the raw JSON before deserialising. Without
        // this, variables nested inside the JSON (e.g. a containerImage referencing a
        // package variable) stay as literal `#{...}` text in the typed objects.
        // Variables that contain JSON-significant characters must use `| JsonEscape`
        // when referenced so the substituted text stays parseable.
        var evaluatedJson = variables.Evaluate(variableJson);

        try
        {
            var output = JsonConvert.DeserializeObject<T>(evaluatedJson, CalamariContractSerializationSettings.Default);
            return output ?? throw new CommandException($"Variable {name} was deserialized as null ");
        }
        catch (JsonSerializationException)
        {
            throw new CommandException($"Variable {name} could not be deserialized as type {typeof(T).FullName}");
        }
        catch (JsonReaderException)
        {
            throw new CommandException($"Variable {name} was not valid JSON or could not be deserialized");
        }
    }
}