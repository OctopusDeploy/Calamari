package octopus.main.buildtypes

import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.nunit

class CalamariOnlyTestBuildType(block: BuildType.() -> Unit) : TestBuildType({
    steps {
        nunit {
            name = "Run Calamari Tests"
            nunitPath = "%teamcity.tool.NUnit.Console.DEFAULT%"
            includeTests = "CalamariTests/Calamari.%CalamariFlavour%.Tests.dll"
        }
    }
}) {
    init {
        this.apply(block)
    }
}