namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public class StackFormationNamedOutput
    {
        public string Name { get; }

        public StackFormationNamedOutput(string name)
        {
            Name = name;
        }
    }
}