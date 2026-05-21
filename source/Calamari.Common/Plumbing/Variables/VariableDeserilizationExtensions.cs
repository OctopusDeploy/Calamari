using System;
using Calamari.Common.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Calamari.Common.Plumbing.Variables;

public static class CalamariVariableDeserilisationExtensions
{
    public static readonly JsonSerializerSettings DefaultSerializationSettings = new()
    {
        Formatting = Formatting.None,
        ContractResolver = new CamelCasePropertyNamesContractResolver(),
        MissingMemberHandling = MissingMemberHandling.Error,
    };
    
    public static T GetValueDeserilisedAs<T>(this CalamariVariables variables, string name)
    {
        IVariables castVariables = variables;
        return castVariables.GetValueDeserilisedAs<T>(name);
    }

    public static T GetValueDeserilisedAs<T>(this IVariables variables, string name)
    {
        var variableJson = variables.Get(name);

        if (string.IsNullOrEmpty(variableJson))
        {
            throw new CommandException($"Variable {name} was not supplied");
        }

        try
        {
            var output = JsonConvert.DeserializeObject<T>(variableJson, DefaultSerializationSettings);
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