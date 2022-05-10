package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.*
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.commitStatusPublisher

fun BuildType.includeVcs(): BuildType {
    this.vcs {
        root(AbsoluteId("OctopusDeploy_LIbraries_Sashimi_SharedGitHubVcsRoot"))

        excludeDefaultBranchChanges = true
        showDependenciesChanges = true
    }
    return this
}

fun BuildType.commitStatusPublisher(): BuildType {
    this.features {
        commitStatusPublisher {
            publisher = github {
                githubUrl = "https://api.github.com"
                authType = personalToken {
                    token = "credentialsJSON:d2d6ff31-56f1-4893-a448-f7a517da6c88"
                }
            }
        }
    }
    return this
}

fun BuildType.githubPullRequests(): BuildType {
    this.features {
        pullRequests {
            provider = github {
                authType = token {
                    token = "credentialsJSON:e3abf97f-cad5-4d88-9a7a-f588c55c53ed"
                }
                filterAuthorRole = PullRequests.GitHubRoleFilter.MEMBER
            }
        }
    }
    return this
}

