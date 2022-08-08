using Calamari.Aws.Deployment.Conventions;

namespace Calamari.Aws.Integration.CloudFormation
{
    public class RunningChangeSet
    {
        public StackArn Stack { get; }
        public ChangeSetArn ChangeSet { get; }

        public RunningChangeSet(StackArn stack, ChangeSetArn changeSet)
        {
            Stack = stack;
            ChangeSet = changeSet;
        }
    }
}