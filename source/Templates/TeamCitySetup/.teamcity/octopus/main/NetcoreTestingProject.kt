package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.Project
import jetbrains.buildServer.configs.kotlin.v2019_2.toId
import octopus.main.buildtypes.NetCoreTestBuildType

class NetcoreTestingProject : Project({
    var projectName = "Netcore Testing"
    name = projectName

    val buildTypesToRegister = sequence {
        val items = listOf(
                ("Amazon Linux 2" to "AmazonLinux2"),
                ("Ubuntu 18.04 LTS" to "Ubuntu18"),
                ("open SUSE 15.1" to "openSUSE151"),
                ("open SUSE 12" to "SUSE12"),
                ("CentOS 7" to "CentOS7"),
                ("Fedora" to "Fedora32"),
                ("Debian" to "Debian913"),
                ("RHEL 7.2" to "RHEL72")
        )
        for (item in items) {
            yield(NetCoreTestBuildType {
                id(item.second.toId(projectName.toId()))
                name = item.first
                params {
                    param("dotnet_runtime", "linux-x64")
                }
                requirements {
                    equals("system.Octopus.AgentType", item.second)
                    equals("system.Octopus.Purpose", "Test")
                    exists("system.Octopus.DotnetSdk3.1")
                }
            })
        }
        yield(NetCoreTestBuildType {
            id("Windows".toId(projectName.toId()))
            name = "Windows"
            params {
                param("dotnet_runtime", "win-x64")
                param("VSTest_TestCaseFilter", "TestCategory!=macOs & TestCategory!=Nix & TestCategory!=PlatformAgnostic & TestCategory != nixMacOS")
            }
            requirements {
                equals("system.Octopus.OS", "Windows")
                doesNotEqual("system.Octopus.OSVersion", "2008")
                doesNotEqual("system.Octopus.OSVersion", "2008R2")
                equals("system.Octopus.Purpose", "Test")
                exists("system.Octopus.DotnetSdk3.1")
            }
        })
        yield(NetCoreTestBuildType {
            id("Mac OSX".toId(projectName.toId()))
            name = "Mac OSX 10.15.2"
            params {
                param("dotnet_runtime", "osx-x64")
            }
            requirements {
                exists("DotNetCLI")
                equals("teamcity.agent.jvm.os.name", "Mac OS X")
            }
        })
    }

    buildTypesToRegister.forEach { buildType(it.commitStatusPublisher()) }
    buildTypesOrder = buildTypes.toList()

    params {
        param("VSTest_TestCaseFilter", "TestCategory != Windows")
        param("dotnet.cli.test.reporting", "off")
    }
})

