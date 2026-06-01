namespace Calamari.Aws.Integration.CloudFormation.Templates;

// Can we delete this?
public class StackFormationNamedOutput(string name)
{
    public string Name { get; } = name;
}