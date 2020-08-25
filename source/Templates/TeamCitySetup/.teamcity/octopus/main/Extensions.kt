package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.*
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.commitStatusPublisher

fun BuildType.includeVcs(): BuildType {
    this.vcs {
        root(AbsoluteId("SharedGitHubVcsRoot"))

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
                    token = "%commitStatusPublisher.apiKey%"
                }
            }
        }
    }
    return this
}

