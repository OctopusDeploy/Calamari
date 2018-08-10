namespace Calamari.Aws.Integration.CloudFormation
{
    public class ChangeSetArn
    {
        public string Value { get; }

        public ChangeSetArn(string value)
        {
            Value = value;
        }
    }
}