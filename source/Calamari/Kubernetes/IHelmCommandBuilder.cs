using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Octostache;

namespace Calamari.Kubernetes
{
    public interface IHelmCommandBuilder
    {
        IHelmCommandBuilder WithCommand(string command);
        IHelmCommandBuilder Namespace(string @namespace);
        IHelmCommandBuilder NamespaceFromSpecialVariable(RunningDeployment deployment);
        IHelmCommandBuilder ResetValues();
        IHelmCommandBuilder ResetValuesFromSpecialVariableFlag(RunningDeployment deployment);
        IHelmCommandBuilder TillerTimeout(string tillerTimeout);
        IHelmCommandBuilder TillerTimeoutFromSpecialVariable(RunningDeployment deployment);
        IHelmCommandBuilder TillerNamespace(string tillerNamespace);
        IHelmCommandBuilder TillerNamespaceFromSpecialVariable(RunningDeployment deployment);
        IHelmCommandBuilder Timeout(string timeout);
        IHelmCommandBuilder TimeoutFromSpecialVariable(RunningDeployment deployment);
        IHelmCommandBuilder Values(string values);
        IHelmCommandBuilder ValuesFromSpecialVariable(RunningDeployment deployment, ICalamariFileSystem fileSystem);
        IHelmCommandBuilder AdditionalArguments(string additionalArguments);
        IHelmCommandBuilder AdditionalArgumentsFromSpecialVariable(RunningDeployment deployment);
        IHelmCommandBuilder Purge();
        IHelmCommandBuilder Install();
        IHelmCommandBuilder Home(string homeDirectory);
        IHelmCommandBuilder ClientOnly();
        IHelmCommandBuilder Version();
        IHelmCommandBuilder Destination(string destinationDirectory);
        IHelmCommandBuilder Username(string username);
        IHelmCommandBuilder Password(string password);
        IHelmCommandBuilder Debug();
        IHelmCommandBuilder SetExecutable(VariableDictionary variableDictionary);
        IHelmCommandBuilder Reset();
        string Build();
    }
}