namespace Calamari.Aws.Integration.CloudFormation;

public class VariableOutput(string name, string value)
{
    public string Name { get; } = name;
    public string Value { get; } = value;
}