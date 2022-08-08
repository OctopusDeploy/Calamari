namespace Calamari.Aws.Integration.CloudFormation
{
    public class StackArn
    {
        public StackArn(string value)
        {
            Value = value;
        }

        public string Value { get; }
    }
}