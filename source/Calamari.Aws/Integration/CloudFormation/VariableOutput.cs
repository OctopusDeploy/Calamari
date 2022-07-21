namespace Calamari.Aws.Integration.CloudFormation
{
    public class VariableOutput
    {
        public VariableOutput(string name, string value)
        {
            Name = name;
            Value = value;
        }
        
        public string Name { get; }
        public string Value { get; }
    }
}