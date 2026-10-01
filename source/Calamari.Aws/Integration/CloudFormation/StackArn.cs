namespace Calamari.Aws.Integration.CloudFormation;

public class StackArn(string value)
{
    public string Value { get; } = value;
}