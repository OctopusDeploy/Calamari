using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Calamari.Common.Util;

namespace Calamari.Aws.Integration.Ecs.Deploy;

public interface IEcsTemplateParameter
{
    string Name { get; }
    // Typed value handed to CDK's CfnParameter Default — preserves the underlying
    // type so a Number param emits a JSON number literal, not a quoted string.
    object Default { get; }
    // String form for the AWS SDK Parameter override list — required to be a string
    // even for Number-typed params (CFN parses it back).
    string Value { get; }
    string CfnType { get; }
}

public record EcsTemplateParameter<T>(string Name, T TypedValue) : IEcsTemplateParameter
{
    public object Default => TypedValue;

    // Invariant culture so "1.5" doesn't become "1,5" in non-English locales — CFN
    // and the AWS API both expect invariant-formatted numbers.
    public string Value => TypedValue switch
    {
        null            => string.Empty,
        IFormattable f  => f.ToString(null, CultureInfo.InvariantCulture),
        _               => TypedValue.ToString() ?? string.Empty
    };

    public string CfnType => typeof(T) == typeof(double) ? "Number" : "String";
}

// Static factory enables generic type inference at the call site:
//   EcsTemplateParameter.Of(name, "string value") 
//   EcsTemplateParameter.Of(name, 1.0) 
public static class EcsTemplateParameter
{
    public static EcsTemplateParameter<T> Of<T>(string name, T value) => new(name, value);
}

public class ListTemplateInputs<TInput>(IEnumerable<TInput> inputs) : ITemplateInputs<TInput>
{
    public IEnumerable<TInput> Inputs { get; } = inputs.ToList();
}
