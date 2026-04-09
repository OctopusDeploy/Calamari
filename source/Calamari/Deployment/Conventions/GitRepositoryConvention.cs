using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Git;
using Calamari.CommitToGit;
using Calamari.Common.Commands;

namespace Calamari.Deployment.Conventions;

public class GitRepositoryConvention : IInstallConvention
{
    readonly DeploymentConfigFactory configFactory;
    readonly RepositoryFactory repositoryFactory;

    public GitRepositoryConvention(DeploymentConfigFactory configFactory, RepositoryFactory repositoryFactory)
    {
        this.configFactory = configFactory;
        this.repositoryFactory = repositoryFactory;
    }

    public void Install(RunningDeployment deployment)
    {
        var repositoryConfig = configFactory.CreateCommitToGitRepositoryConfig(deployment);
        
        using var repository = repositoryFactory.CloneRepository("git_repository", repositoryConfig.gitConnection);
        
        
        //copy in the 
        

    }
}