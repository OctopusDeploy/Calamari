#nullable enable
using System;
using System.Linq;
using Calamari.ArgoCD.Conventions.UpdateImageTag;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.Git.PullRequests;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Deployment.Conventions;
using Calamari.Integration.Time;
using Octopus.Calamari.Contracts.ArgoCD;

namespace Calamari.ArgoCD.Conventions
{
    public class UpdateArgoCDAppImagesInstallConvention : IInstallConvention
    {
        readonly ICalamariFileSystem fileSystem;
        readonly ILog log;
        readonly DeploymentConfigFactory deploymentConfigFactory;
        readonly ICustomPropertiesLoader customPropertiesLoader;
        readonly IArgoCDApplicationManifestParser argoCdApplicationManifestParser;
        readonly IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver;
        readonly IClock clock;
        readonly IArgoCDFilesUpdatedReporter reporter;
        readonly ArgoCDOutputVariablesWriter outputVariablesWriter;

        public UpdateArgoCDAppImagesInstallConvention(
            ILog log,
            ICalamariFileSystem fileSystem,
            DeploymentConfigFactory deploymentConfigFactory,
            ICustomPropertiesLoader customPropertiesLoader,
            IArgoCDApplicationManifestParser argoCdApplicationManifestParser,
            IGitVendorPullRequestClientResolver gitVendorPullRequestClientResolver,
            IClock clock,
            IArgoCDFilesUpdatedReporter reporter,
            ArgoCDOutputVariablesWriter outputVariablesWriter)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.deploymentConfigFactory = deploymentConfigFactory;
            this.customPropertiesLoader = customPropertiesLoader;
            this.argoCdApplicationManifestParser = argoCdApplicationManifestParser;
            this.gitVendorPullRequestClientResolver = gitVendorPullRequestClientResolver;
            this.clock = clock;
            this.reporter = reporter;
            this.outputVariablesWriter = outputVariablesWriter;
        }

        public void Install(RunningDeployment deployment)
        {
            log.Verbose("Executing Update Argo CD Application Images");
            var deploymentConfig = deploymentConfigFactory.CreateUpdateImageConfig(deployment);

            var repositoryFactory = new RepositoryFactory(log,
                                                          fileSystem,
                                                          deployment.CurrentDirectory,
                                                          gitVendorPullRequestClientResolver,
                                                          clock);

            var argoProperties = customPropertiesLoader.Load<ArgoCDCustomPropertiesDto>();
            var gitCredentials = argoProperties.Credentials.ToDictionary(c => c.Url);
            var authenticatingRepositoryFactory = new AuthenticatingRepositoryFactory(gitCredentials, repositoryFactory, log);
            var deploymentScope = deployment.Variables.GetDeploymentScope();

            log.LogApplicationCounts(deploymentScope, argoProperties.Applications);

            var appUpdater = new ApplicationUpdater(deploymentScope,
                                                    deploymentConfig,
                                                    authenticatingRepositoryFactory,
                                                    log,
                                                    fileSystem,
                                                    argoCdApplicationManifestParser,
                                                    new ImageTagUpdateCommitMessageGenerator(deploymentConfig.CommitParameters.Description),
                                                    outputVariablesWriter);

            var applicationResults = argoProperties.Applications
                                                   .Select(application =>
                                                           {
                                                               var gateway = argoProperties.Gateways.Single(g => g.Id == application.GatewayId);
                                                               return appUpdater.ProcessApplication(application, gateway);
                                                           })
                                                   .ToList();

            //if we are creating a pull request, we don't want to report files updated (as this will be passed down as output variables _with_ the PR info)

                reporter.ReportFilesUpdated(deploymentConfig.CommitParameters, applicationResults);

            var totalApplicationsWithSourceCounts = applicationResults.Select(r => (r.ApplicationName, r.TotalSourceCount, r.MatchingSourceCount)).ToList();
            var updatedApplications = applicationResults.Where(r => r.Updated).ToList();
            var updatedApplicationsWithSources = updatedApplications.Select(r => (r.ApplicationName, r.UpdatedSourceCount)).ToList();
            var gitReposUpdated = updatedApplications.SelectMany(r => r.GitReposUpdated).ToHashSet();
            var newImagesWritten = updatedApplications.SelectMany(r => r.UpdatedImages).ToHashSet();

            var gatewayIds = argoProperties.Applications.Select(a => a.GatewayId).ToHashSet();
            outputVariablesWriter.WriteImageUpdateOutput(gatewayIds,
                                                         gitReposUpdated,
                                                         totalApplicationsWithSourceCounts,
                                                         updatedApplicationsWithSources,
                                                         newImagesWritten.Count
                                                        );
        }
    }
}