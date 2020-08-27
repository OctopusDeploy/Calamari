package octopus.main.buildtypes

import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.nuGetPublish
import jetbrains.buildServer.configs.kotlin.v2019_2.triggers.vcs

object PublishToFeedzIo : BuildType({
    name = "Publish to Feedz.io"

    buildNumberPattern = "${Build.depParamRefs.buildNumber}"

    steps {
        nuGetPublish {
            name = "Nuget Publish"
            toolPath = "%teamcity.tool.NuGet.CommandLine.DEFAULT%"
            packages = "*.nupkg"
            serverUrl = "%InternalNuget.OctopusDependeciesFeedUrl%"
            apiKey = "%nuGetPublish.apiKey%"
            args = "-Timeout 1200"
        }
    }

    triggers {
        vcs {
            branchFilter = """
                ## We actually want to publish all builds
                +:refs/tags/*
                +:<default>
                +:refs/heads/*
            """.trimIndent()
        }
    }

    dependencies {
        dependency(Build) {
            artifacts {
                cleanDestination = true
                artifactRules = "*.nupkg"
            }
        }
    }

    requirements {
        equals("system.Octopus.Purpose", "Build")
    }
})