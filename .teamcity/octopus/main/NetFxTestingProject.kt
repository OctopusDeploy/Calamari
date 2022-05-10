package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.BuildType
import jetbrains.buildServer.configs.kotlin.v2019_2.Project
import jetbrains.buildServer.configs.kotlin.v2019_2.toId
import octopus.main.buildtypes.DotNetTestBuildType
import octopus.main.buildtypes.CalamariOnlyTestBuildType

class NetFxTestingProject : Project({
    val projectName = "Windows(NetFx) Testing"
    name = projectName

    val buildTypesToRegister = sequence {
        val items = listOf(
                ("Windows 2008" to "2008"),
                ("Windows 2008 R2" to "2008R2"),
                ("Windows 2012" to "2012"),
                ("Windows 2012 R2" to "2012R2"),
                ("Windows 2016" to "2016"),
                ("Windows 2019" to "2019")
        )
        for (item in items) {

            val block: BuildType.() -> Unit = {
                id(item.second.toId(projectName.toId()))
                name = item.first
                requirements {
                    equals("system.Octopus.OS", "Windows")
                    equals("system.Octopus.Purpose", "Test")
                    equals("system.Octopus.OSVersion", item.second)
                }
            }
            if(item.second.startsWith("2008")) {
                yield(CalamariOnlyTestBuildType(block))
            } else {
                yield(DotNetTestBuildType(block))
            }
        }
    }

    buildTypesToRegister.forEach { buildType(it.commitStatusPublisher().githubPullRequests()) }
    buildTypesOrder = buildTypes.toList()

    params {
        param("VSTest_TestCaseFilter", "TestCategory!=macOs & TestCategory!=Nix & TestCategory != nixMacOS")
        param("dotnet_runtime", "netfx")
    }
})