package octopus.main.buildtypes

import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.Swabra
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.freeDiskSpace
import jetbrains.buildServer.configs.kotlin.v2019_2.buildFeatures.swabra
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.powerShell

object Build : BuildType({
    name = "Build"
    description = "Build the open source Sashimi.NamingIsHard package"

    params {
        param("env.HOMEDRIVE", "C:")
        param("env.HOMEPATH", """\users\administrator""")
        param("system.OctopusPackageVersion", "%build.number%")
        param("env.CALAMARI_GITHUB_AUTHUSERNAME", "OctopusGithubTester")
    }

    steps {
        powerShell {
            name = "Build"
            scriptMode = script {
                content = """
                    ./build.ps1

                    exit ${'$'}LASTEXITCODE
                """.trimIndent()
            }
        }
    }

    features {
        freeDiskSpace {
            requiredSpace = "1gb"
            failBuild = false
        }
        swabra {
            filesCleanup = Swabra.FilesCleanup.AFTER_BUILD
            lockingProcesses = Swabra.LockingProcessPolicy.KILL
        }
    }

    requirements {
        equals("system.Octopus.OS", "Windows")
        equals("system.Octopus.Purpose", "Build")
        exists("system.Octopus.DotnetSdk3.1")
        exists("system.Octopus.DotnetSdk472")
    }
})