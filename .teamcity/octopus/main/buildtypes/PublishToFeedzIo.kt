package octopus.main.buildtypes

import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.nuGetPublish
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.vcs

object PublishToFeedzIo : BuildType({
    name = "Chain: Build and Test and Publish to Feedz.io"

    buildNumberPattern = "${Build.depParamRefs.buildNumber}"

    steps {
        nuGetPublish {
            name = "Nuget Publish"
            toolPath = "%teamcity.tool.NuGet.CommandLine.DEFAULT%"
            packages = "*.nupkg"
            serverUrl = "%InternalNuget.OctopusDependeciesFeedUrl%"
            apiKey = "credentialsJSON:a7d4426a-7256-4df7-a953-266292e6ad81"
            args = "-Timeout 1200"
        }
    }

    triggers {
        vcs {
            branchFilter = """
                +:<default>
                +:pull/*
                +:refs/tags/*
            """.trimIndent()
        }
    }

    dependencies {
        dependency(Build) {
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
            }

            artifacts {
                cleanDestination = true
                artifactRules = "*.nupkg"
            }
        }

        dependency(RelativeId("NetcoreTesting_AmazonLinux")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_Ubuntu")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_OpenSUSE")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_CentOS")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_Fedora")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_Debian")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_Rhel")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("NetcoreTesting_Windows")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }

        dependency(RelativeId("WindowsNetFxTesting_2012")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("WindowsNetFxTesting_2012r2")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("WindowsNetFxTesting_2016")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
        dependency(RelativeId("WindowsNetFxTesting_2019")){
            snapshot {
                onDependencyFailure = FailureAction.CANCEL
                onDependencyCancel = FailureAction.CANCEL
            }
        }
    }

    requirements {
        startsWith("system.agent.name", "nautilus-")
    }
})