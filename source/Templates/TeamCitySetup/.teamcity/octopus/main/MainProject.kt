package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.Project
import jetbrains.buildServer.configs.kotlin.v2019_2.sequential
import octopus.main.buildtypes.Build
import octopus.main.buildtypes.PublishToFeedzIo

object MainProject : Project({
    val subProjects = mutableListOf<Project>()
    //#if includeDotnetTests
    subProjects.add(NetcoreTestingProject())
    //#endif
    //#if includeNetFxTests
    subProjects.add(NetFxTestingProject())
    //#endif
    //#if includeMonoTests
    subProjects.add(MonoTestingProject())
    //#endif

    val buildTypesToRegister = arrayOf(
            Build,
            PublishToFeedzIo
    )

    buildTypesToRegister.forEach { buildType(it.includeVcs().commitStatusPublisher()) }
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
        param("CalamariFlavour", "NamingIsHard")
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