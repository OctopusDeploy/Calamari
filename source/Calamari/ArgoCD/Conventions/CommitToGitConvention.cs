using Calamari.Common.Commands;
using Calamari.Deployment.Conventions;

namespace Calamari.ArgoCD.Conventions;

public class CommitToGitConvention : IInstallConvention
{
    readonly string sourceDirectory;

    public CommitToGitConvention(string sourceDirectory)
    {
        this.sourceDirectory = sourceDirectory;
    }

    public void Install(RunningDeployment deployment)
    {
        //get git repository from variables
        
        throw new System.NotImplementedException();
    }
}