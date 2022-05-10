package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.Project
import jetbrains.buildServer.configs.kotlin.v2019_2.sequential
import octopus.main.buildtypes.Build
import octopus.main.buildtypes.PublishToFeedzIo

object MainProject : Project({
    val subProjects = mutableListOf<Project>()
    subProjects.add(NetcoreTestingProject())
    subProjects.add(NetFxTestingProject())

    val buildTypesToRegister = arrayOf(
            Build,
            PublishToFeedzIo
    )

    buildTypesToRegister.forEach { buildType(it.includeVcs().commitStatusPublisher().githubPullRequests()) }
    buildTypesOrder = buildTypesToRegister.toList()

    sequential {
        buildType(Build)
        if (subProjects.count() > 0) {
            parallel {
                subProjects.forEach {
                    it.buildTypes.forEach { bt ->
                        buildType(bt)
                    }
                }
            }
        }
        buildType(PublishToFeedzIo)
    }

    params {
        param("CalamariFlavour", "AzureAppService")
        param("RepositoryName", "Sashimi.%CalamariFlavour%")
        param("DefaultGitBranch", "main")
    }

    subProjects.forEach {
        subProject(it)
        it.buildTypes.forEach { bt ->
            with(bt.dependencies.items[0]) {
                artifacts {
                    cleanDestination = true
                    artifactRules = "Sashimi.%CalamariFlavour%.Tests.zip!**=>SashimiTests"
                }
                artifacts {
                    cleanDestination = true
                    artifactRules = "Calamari.%CalamariFlavour%.Tests.zip!%dotnet_runtime%/**=>CalamariTests"
                }
                artifacts {
                    cleanDestination = true
                    artifactRules = "Calamari.%CalamariFlavour%.zip!%dotnet_runtime%/**=>CalamariBinaries"
                }
            }
        }
    }
})