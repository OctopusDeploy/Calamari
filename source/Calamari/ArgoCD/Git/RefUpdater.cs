using System.Collections.Generic;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Domain;
using Calamari.ArgoCD.Dtos;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.ArgoCD.Git;

public class RefUpdater : BaseUpdater
{
    public RefUpdater(Application applicationFromYaml, Dictionary<string, GitCredentialDto> gitCredentials, RepositoryFactory repositoryFactory, UpdateArgoCDAppDeploymentConfig deploymentConfig,
                      string defaultRegistry,
                      ArgoCDGatewayDto gateway,
                      ILog log,
                      ICommitMessageGenerator commitMessageGenerator,
                      ICalamariFileSystem fileSystem,
                      ArgoCDOutputVariablesWriter outputVariablesWriter) : base(repositoryFactory, gitCredentials, log, commitMessageGenerator,
                                                                                fileSystem)
    {
    }

    public override SourceUpdateResult Process(ApplicationSourceWithMetadata sourceWithMetadata)
    {
        throw new System.NotImplementedException();
    }
}   