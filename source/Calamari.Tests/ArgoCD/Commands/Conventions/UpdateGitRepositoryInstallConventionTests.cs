#if NET
using System;
using System.IO;
using System.Linq;
using Calamari.ArgoCD.Commands;
using Calamari.ArgoCD.Conventions;
using Calamari.ArgoCD.Dtos;
using Calamari.ArgoCD.Git;
using Calamari.ArgoCD.GitHub;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Testing.Helpers;
using Calamari.Tests.ArgoCD.Git;
using Calamari.Tests.Fixtures.Integration.FileSystem;
using FluentAssertions;
using LibGit2Sharp;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.ArgoCD.Commands.Conventions
{
    [TestFixture]
    public class UpdateGitRepositoryInstallConventionTests
    {
        readonly ICalamariFileSystem fileSystem = TestCalamariPhysicalFileSystem.GetPhysicalFileSystem();
        InMemoryLog log;
        string tempDirectory;
        string WorkingDirectory => Path.Combine(tempDirectory, "working");
        string PackageDirectory => Path.Combine(WorkingDirectory, CommitToGitCommand.PackageDirectoryName);
        
        string OriginPath => Path.Combine(tempDirectory, "origin");

        GitBranchName argoCdBranchName = new GitBranchName("devBranch");

        [SetUp]
        public void Init()
        {
            log = new InMemoryLog();
            tempDirectory = fileSystem.CreateTemporaryDirectory();
            Directory.CreateDirectory(PackageDirectory);

            RepositoryHelpers.CreateBareRepository(OriginPath);
            RepositoryHelpers.CreateBranchIn(argoCdBranchName, OriginPath);
        }
        
        [TearDown]
        public void Cleanup()
        {
            fileSystem.DeleteDirectory(tempDirectory, FailureOptions.IgnoreFailure);
        }

        [Test]
        public void ExecuteCopiesFilesFromPackageIntoRepo()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);
            const string nestedFilename = "nested/second.yaml";
            CreateFileUnderPackageDirectory(nestedFilename);
            
            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.Recursive] = "True",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this"
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;

            var customPropertiesLoader = SetupCustomPropertiesLoader();

            var convention = new UpdateGitRepositoryInstallConvention(fileSystem, CommitToGitCommand.PackageDirectoryName, log, Substitute.For<IGitHubPullRequestCreator>(), new ArgoCommitToGitConfigFactory(nonSensitiveCalamariVariables), customPropertiesLoader);
            convention.Install(runningDeployment);

            var resultPath = CloneOrigin();
            var resultFirstContent = File.ReadAllText(Path.Combine(resultPath, firstFilename));
            var resultNestedContent = File.ReadAllText(Path.Combine(resultPath, nestedFilename));
            resultFirstContent.Should().Be(firstFilename);
            resultNestedContent.Should().Be(nestedFilename);
        }

        ICustomPropertiesLoader SetupCustomPropertiesLoader()
        {
            var customPropertiesFactory = Substitute.For<ICustomPropertiesLoader>();
            customPropertiesFactory.Load<ArgoCDCustomPropertiesDto>().Returns(new ArgoCDCustomPropertiesDto(new[]
            {
                new ArgoCDApplicationDto("Gateway1", "App1", new[]
                {
                    new ArgoCDApplicationSourceDto(OriginPath, argoCdBranchName.Value, "")
                }, AppManifest)
            }, new GitCredentialDto[]
            {
                new GitCredentialDto("https://github.com/OctopusDeploy/argo-gitops-frank", "user", "password")
            }));
            return customPropertiesFactory;
        }

        [Test]
        public void DoesNotCopyFilesRecursivelyIfNotSet()
        {
            const string firstFilename = "first.yaml";
            CreateFileUnderPackageDirectory(firstFilename);
            const string nestedFilename = "nested/second.yaml";
            CreateFileUnderPackageDirectory(nestedFilename);
            
            var nonSensitiveCalamariVariables = new NonSensitiveCalamariVariables()
            {
                [KnownVariables.OriginalPackageDirectoryPath] = WorkingDirectory,
                [SpecialVariables.Git.InputPath] = "",
                [SpecialVariables.Git.Recursive] = "False",
                [SpecialVariables.Git.CommitMethod] = "DirectCommit",
                [SpecialVariables.Git.CommitMessageSummary] = "Octopus did this"
            };
            var allVariables = new CalamariVariables();
            allVariables.Merge(nonSensitiveCalamariVariables);

            var runningDeployment = new RunningDeployment("./arbitraryFile.txt", allVariables);
            runningDeployment.CurrentDirectoryProvider = DeploymentWorkingDirectory.StagingDirectory;
            runningDeployment.StagingDirectory = WorkingDirectory;    
            
            var customPropertiesLoader = SetupCustomPropertiesLoader();

            var convention = new UpdateGitRepositoryInstallConvention(fileSystem, CommitToGitCommand.PackageDirectoryName, log, Substitute.For<IGitHubPullRequestCreator>(), new ArgoCommitToGitConfigFactory(nonSensitiveCalamariVariables), customPropertiesLoader);
            convention.Install(runningDeployment);
            
            var resultPath = CloneOrigin();
            File.Exists(Path.Combine(resultPath, firstFilename)).Should().BeTrue();
            File.Exists(Path.Combine(resultPath, nestedFilename)).Should().BeFalse();
        }

        //Accepts a relative path and creates a file under the package directory, which
        //contains the relative filename as its contents.
        void CreateFileUnderPackageDirectory(string filename)
        {
            var packageFile = Path.Combine(PackageDirectory, filename);
            var directory = Path.GetDirectoryName(packageFile);
            if (directory != null)
            {
                Directory.CreateDirectory(directory);        
            }

            File.WriteAllText(packageFile, filename);
        }

        string CloneOrigin()
        {
            var subPath = Guid.NewGuid().ToString();
            var resultPath = Path.Combine(tempDirectory, subPath);
            Repository.Clone(OriginPath, resultPath);
            var resultRepo = new Repository(resultPath);
            LibGit2Sharp.Commands.Checkout(resultRepo, $"origin/{argoCdBranchName}");

            return resultPath;
        }
        
            string AppManifest =
        "{\n  \"metadata\" : {\n    \"name\" : \"frank-gitops\",\n    \"namespace\" : \"argocd\",\n    \"uid\" : \"ac63e32e-16f0-40d4-b706-1b8a2f8b6a3f\",\n    \"resourceVersion\" : \"17761769\",\n    \"generation\" : 59448,\n    \"creationTimestamp\" : \"2024-11-05T00:47:11Z\",\n    \"labels\" : {\n      \"k8slens-edit-resource-version\" : \"v1alpha1\"\n    },\n    \"annotations\" : {\n      \"argo.octopus.com/environment\" : \"dev\",\n      \"argo.octopus.com/project\" : \"argo-image-update\",\n      \"creationTimestamp\" : \"2024-11-05T00:47:11Z\",\n      \"octopus.com/environment\" : \"dev\",\n      \"octopus.com/project\" : \"argo-image-update\"\n    },\n    \"managedFields\" : [ {\n      \"manager\" : \"node-fetch\",\n      \"operation\" : \"Update\",\n      \"apiVersion\" : \"argoproj.io/v1alpha1\",\n      \"time\" : \"2025-08-01T01:37:39Z\",\n      \"fieldsType\" : \"FieldsV1\",\n      \"fieldsV1\" : {\n        \"f:metadata\" : {\n          \"f:annotations\" : {\n            \".\" : { },\n            \"f:argo.octopus.com/environment\" : { },\n            \"f:argo.octopus.com/project\" : { },\n            \"f:creationTimestamp\" : { },\n            \"f:octopus.com/environment\" : { },\n            \"f:octopus.com/project\" : { }\n          },\n          \"f:labels\" : {\n            \".\" : { },\n            \"f:k8slens-edit-resource-version\" : { }\n          }\n        }\n      }\n    }, {\n      \"manager\" : \"argocd-server\",\n      \"operation\" : \"Update\",\n      \"apiVersion\" : \"argoproj.io/v1alpha1\",\n      \"time\" : \"2025-08-07T23:38:34Z\",\n      \"fieldsType\" : \"FieldsV1\",\n      \"fieldsV1\" : {\n        \"f:spec\" : {\n          \".\" : { },\n          \"f:destination\" : {\n            \".\" : { },\n            \"f:namespace\" : { },\n            \"f:server\" : { }\n          },\n          \"f:project\" : { },\n          \"f:source\" : {\n            \".\" : { },\n            \"f:path\" : { },\n            \"f:repoURL\" : { },\n            \"f:targetRevision\" : { }\n          }\n        },\n        \"f:status\" : {\n          \".\" : { },\n          \"f:health\" : { },\n          \"f:summary\" : { },\n          \"f:sync\" : {\n            \".\" : { },\n            \"f:comparedTo\" : {\n              \".\" : { },\n              \"f:destination\" : { },\n              \"f:source\" : { }\n            }\n          }\n        }\n      }\n    }, {\n      \"manager\" : \"argocd-application-controller\",\n      \"operation\" : \"Update\",\n      \"apiVersion\" : \"argoproj.io/v1alpha1\",\n      \"time\" : \"2025-09-09T06:31:25Z\",\n      \"fieldsType\" : \"FieldsV1\",\n      \"fieldsV1\" : {\n        \"f:status\" : {\n          \"f:conditions\" : { },\n          \"f:controllerNamespace\" : { },\n          \"f:health\" : {\n            \"f:status\" : { }\n          },\n          \"f:history\" : { },\n          \"f:operationState\" : {\n            \".\" : { },\n            \"f:finishedAt\" : { },\n            \"f:message\" : { },\n            \"f:operation\" : {\n              \".\" : { },\n              \"f:initiatedBy\" : {\n                \".\" : { },\n                \"f:username\" : { }\n              },\n              \"f:retry\" : { },\n              \"f:sync\" : {\n                \".\" : { },\n                \"f:revision\" : { },\n                \"f:syncStrategy\" : {\n                  \".\" : { },\n                  \"f:hook\" : { }\n                }\n              }\n            },\n            \"f:phase\" : { },\n            \"f:startedAt\" : { },\n            \"f:syncResult\" : {\n              \".\" : { },\n              \"f:resources\" : { },\n              \"f:revision\" : { },\n              \"f:source\" : {\n                \".\" : { },\n                \"f:path\" : { },\n                \"f:repoURL\" : { },\n                \"f:targetRevision\" : { }\n              }\n            }\n          },\n          \"f:reconciledAt\" : { },\n          \"f:resourceHealthSource\" : { },\n          \"f:resources\" : { },\n          \"f:summary\" : {\n            \"f:images\" : { }\n          },\n          \"f:sync\" : {\n            \"f:comparedTo\" : {\n              \"f:destination\" : {\n                \"f:namespace\" : { },\n                \"f:server\" : { }\n              },\n              \"f:source\" : {\n                \"f:path\" : { },\n                \"f:repoURL\" : { },\n                \"f:targetRevision\" : { }\n              }\n            },\n            \"f:revision\" : { },\n            \"f:status\" : { }\n          }\n        }\n      }\n    } ]\n  },\n  \"spec\" : {\n    \"source\" : {\n      \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n      \"path\" : \"dev/\",\n      \"targetRevision\" : \"HEAD\"\n    },\n    \"destination\" : {\n      \"server\" : \"https://kubernetes.default.svc\",\n      \"namespace\" : \"argo-gitops-frank\"\n    },\n    \"project\" : \"default\"\n  },\n  \"status\" : {\n    \"resources\" : [ {\n      \"version\" : \"v1\",\n      \"kind\" : \"ConfigMap\",\n      \"namespace\" : \"argo-gitops-frank\",\n      \"name\" : \"argocd-notifications-cm\",\n      \"status\" : \"Unknown\",\n      \"requiresPruning\" : true\n    }, {\n      \"version\" : \"v1\",\n      \"kind\" : \"Secret\",\n      \"namespace\" : \"guestbook\",\n      \"name\" : \"some-secret\",\n      \"status\" : \"Unknown\",\n      \"requiresPruning\" : true\n    }, {\n      \"version\" : \"v1\",\n      \"kind\" : \"Service\",\n      \"namespace\" : \"argo-gitops-frank\",\n      \"name\" : \"guestbook-ui\",\n      \"status\" : \"Unknown\",\n      \"health\" : {\n        \"status\" : \"Healthy\"\n      },\n      \"requiresPruning\" : true\n    }, {\n      \"group\" : \"apps\",\n      \"version\" : \"v1\",\n      \"kind\" : \"Deployment\",\n      \"namespace\" : \"argo-gitops-frank\",\n      \"name\" : \"nginx-deployment\",\n      \"status\" : \"Unknown\",\n      \"health\" : {\n        \"status\" : \"Healthy\"\n      },\n      \"requiresPruning\" : true\n    }, {\n      \"group\" : \"batch\",\n      \"version\" : \"v1\",\n      \"kind\" : \"CronJob\",\n      \"namespace\" : \"argo-gitops-frank\",\n      \"name\" : \"hello\",\n      \"status\" : \"Unknown\",\n      \"health\" : {\n        \"status\" : \"Suspended\",\n        \"message\" : \"CronJob is Suspended\"\n      },\n      \"requiresPruning\" : true\n    }, {\n      \"group\" : \"rbac.authorization.k8s.io\",\n      \"version\" : \"v1\",\n      \"kind\" : \"ClusterRole\",\n      \"name\" : \"argocd-server-temp\",\n      \"status\" : \"Unknown\",\n      \"requiresPruning\" : true\n    } ],\n    \"sync\" : {\n      \"status\" : \"Unknown\",\n      \"comparedTo\" : {\n        \"source\" : {\n          \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n          \"path\" : \"dev/\",\n          \"targetRevision\" : \"HEAD\"\n        },\n        \"destination\" : {\n          \"server\" : \"https://kubernetes.default.svc\",\n          \"namespace\" : \"argo-gitops-frank\"\n        }\n      },\n      \"revision\" : \"HEAD\"\n    },\n    \"health\" : {\n      \"status\" : \"Suspended\"\n    },\n    \"history\" : [ {\n      \"revision\" : \"f23b3a472335d6ed299e2a9cd6e1f11e99581b51\",\n      \"deployedAt\" : \"2024-11-05T01:06:20Z\",\n      \"id\" : 0,\n      \"source\" : {\n        \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n        \"path\" : \".\",\n        \"targetRevision\" : \"HEAD\"\n      },\n      \"deployStartedAt\" : \"2024-11-05T01:06:20Z\",\n      \"initiatedBy\" : {\n        \"username\" : \"admin\"\n      }\n    }, {\n      \"revision\" : \"a12c12ce6330de8734432ecd0a0fe4941f2955b9\",\n      \"deployedAt\" : \"2025-07-07T06:43:17Z\",\n      \"id\" : 1,\n      \"source\" : {\n        \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n        \"path\" : \"dev/\",\n        \"targetRevision\" : \"HEAD\"\n      },\n      \"deployStartedAt\" : \"2025-07-07T06:43:17Z\",\n      \"initiatedBy\" : {\n        \"username\" : \"admin\"\n      }\n    }, {\n      \"revision\" : \"838cf6f05a0121738dd13424a16a5b194dca22de\",\n      \"deployedAt\" : \"2025-08-07T23:15:45Z\",\n      \"id\" : 2,\n      \"source\" : {\n        \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n        \"path\" : \"dev/\",\n        \"targetRevision\" : \"HEAD\"\n      },\n      \"deployStartedAt\" : \"2025-08-07T23:15:36Z\",\n      \"initiatedBy\" : {\n        \"username\" : \"admin\"\n      }\n    }, {\n      \"revision\" : \"3088761e5958df6638ca6f21e14e215d878e1098\",\n      \"deployedAt\" : \"2025-08-07T23:38:35Z\",\n      \"id\" : 3,\n      \"source\" : {\n        \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n        \"path\" : \"dev/\",\n        \"targetRevision\" : \"HEAD\"\n      },\n      \"deployStartedAt\" : \"2025-08-07T23:38:34Z\",\n      \"initiatedBy\" : {\n        \"username\" : \"admin\"\n      }\n    } ],\n    \"conditions\" : [ {\n      \"type\" : \"ComparisonError\",\n      \"message\" : \"Failed to load target state: failed to generate manifest for source 1 of 1: rpc error: code = Unknown desc = failed to set app instance tracking info on manifest: failed to get annotations for apps/v1, Kind=Deployment /nginx-deployment: .metadata.annotations accessor error: contains non-string value in the map under key \\\"foo\\\": <nil> is of the type <nil>, expected string\",\n      \"lastTransitionTime\" : \"2025-09-09T06:27:30Z\"\n    } ],\n    \"reconciledAt\" : \"2025-09-09T06:31:25Z\",\n    \"operationState\" : {\n      \"operation\" : {\n        \"sync\" : {\n          \"revision\" : \"3088761e5958df6638ca6f21e14e215d878e1098\",\n          \"syncStrategy\" : {\n            \"hook\" : { }\n          }\n        },\n        \"initiatedBy\" : {\n          \"username\" : \"admin\"\n        },\n        \"retry\" : { }\n      },\n      \"phase\" : \"Succeeded\",\n      \"message\" : \"successfully synced (all tasks run)\",\n      \"syncResult\" : {\n        \"resources\" : [ {\n          \"group\" : \"\",\n          \"version\" : \"v1\",\n          \"kind\" : \"Secret\",\n          \"namespace\" : \"guestbook\",\n          \"name\" : \"some-secret\",\n          \"status\" : \"Synced\",\n          \"message\" : \"secret/some-secret configured\",\n          \"hookPhase\" : \"Running\",\n          \"syncPhase\" : \"Sync\"\n        }, {\n          \"group\" : \"\",\n          \"version\" : \"v1\",\n          \"kind\" : \"ConfigMap\",\n          \"namespace\" : \"argo-gitops-frank\",\n          \"name\" : \"argocd-notifications-cm\",\n          \"status\" : \"Synced\",\n          \"message\" : \"configmap/argocd-notifications-cm unchanged\",\n          \"hookPhase\" : \"Running\",\n          \"syncPhase\" : \"Sync\"\n        }, {\n          \"group\" : \"rbac.authorization.k8s.io\",\n          \"version\" : \"v1\",\n          \"kind\" : \"ClusterRole\",\n          \"namespace\" : \"argo-gitops-frank\",\n          \"name\" : \"argocd-server-temp\",\n          \"status\" : \"Synced\",\n          \"message\" : \"clusterrole.rbac.authorization.k8s.io/argocd-server-temp reconciled. clusterrole.rbac.authorization.k8s.io/argocd-server-temp unchanged\",\n          \"hookPhase\" : \"Running\",\n          \"syncPhase\" : \"Sync\"\n        }, {\n          \"group\" : \"\",\n          \"version\" : \"v1\",\n          \"kind\" : \"Service\",\n          \"namespace\" : \"argo-gitops-frank\",\n          \"name\" : \"guestbook-ui\",\n          \"status\" : \"Synced\",\n          \"message\" : \"service/guestbook-ui unchanged\",\n          \"hookPhase\" : \"Running\",\n          \"syncPhase\" : \"Sync\"\n        }, {\n          \"group\" : \"apps\",\n          \"version\" : \"v1\",\n          \"kind\" : \"Deployment\",\n          \"namespace\" : \"argo-gitops-frank\",\n          \"name\" : \"nginx-deployment\",\n          \"status\" : \"Synced\",\n          \"message\" : \"deployment.apps/nginx-deployment configured\",\n          \"hookPhase\" : \"Running\",\n          \"syncPhase\" : \"Sync\"\n        }, {\n          \"group\" : \"batch\",\n          \"version\" : \"v1\",\n          \"kind\" : \"CronJob\",\n          \"namespace\" : \"argo-gitops-frank\",\n          \"name\" : \"hello\",\n          \"status\" : \"Synced\",\n          \"message\" : \"cronjob.batch/hello unchanged\",\n          \"hookPhase\" : \"Running\",\n          \"syncPhase\" : \"Sync\"\n        } ],\n        \"revision\" : \"3088761e5958df6638ca6f21e14e215d878e1098\",\n        \"source\" : {\n          \"repoURL\" : \"https://github.com/OctopusDeploy/argo-gitops-frank\",\n          \"path\" : \"dev/\",\n          \"targetRevision\" : \"HEAD\"\n        }\n      },\n      \"startedAt\" : \"2025-08-07T23:38:34Z\",\n      \"finishedAt\" : \"2025-08-07T23:38:35Z\"\n    },\n    \"summary\" : {\n      \"images\" : [ \"busybox:1.28\", \"nginx:1.29\" ]\n    },\n    \"resourceHealthSource\" : \"appTree\",\n    \"controllerNamespace\" : \"argocd\",\n    \"sourceHydrator\" : { }\n  }\n}";

    }
}
#endif 