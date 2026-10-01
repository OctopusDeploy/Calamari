namespace Calamari.Aws.Integration.CloudFormation;

public class RunningChangeSet(StackArn stack, ChangeSetArn changeSet)
{
    public StackArn Stack { get; } = stack;
    public ChangeSetArn ChangeSet { get; } = changeSet;
}