package octopus.main.buildtypes

import jetbrains.buildServer.configs.kotlin.v2019_2.BuildStep
import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.buildSteps.script

class DotNetTestBuildType(block: BuildType.() -> Unit) : TestBuildType({
    steps {
        script {
            name = "Run Sashimi Tests"
            workingDir = "SashimiTests"
            scriptContent = """dotnet vstest Sashimi.%CalamariFlavour%.Tests.dll /TestCaseFilter:"%VSTest_TestCaseFilter%" /logger:trx"""
        }
        script {
            name = "Run Calamari Tests"
            executionMode = BuildStep.ExecutionMode.RUN_ON_FAILURE
            workingDir = "CalamariTests"
            scriptContent = """dotnet vstest Calamari.*.Tests.dll /TestCaseFilter:"%VSTest_TestCaseFilter%" /logger:trx"""
        }
    }

}) {
    init {
        this.apply(block)
    }
}