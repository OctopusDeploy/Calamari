package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.Project
import jetbrains.buildServer.configs.kotlin.v2019_2.toId
import octopus.main.buildtypes.DotNetTestBuildType

class NetcoreTestingProject : Project({
    var projectName = "Netcore Testing"
    name = projectName

    val buildTypesToRegister = sequence {
        val items = listOf(
                ("Amazon Linux" to "AmazonLinux"),
                ("Ubuntu" to "Ubuntu"),
                ("openSUSE Leap" to "openSUSE"),
                ("SUSE LES" to "SLES"),
                ("CentOS" to "CentOS"),
                ("Fedora" to "Fedora"),
                ("Debian" to "Debian"),
                ("RHEL" to "RHEL")
        )
        for (item in items) {
            yield(DotNetTestBuildType {
                id(item.second.toId(projectName.toId()))
                name = item.first
                params {
                    param("dotnet_runtime", "linux-x64")
                }
                requirements {
                    equals("system.Octopus.OS", item.second)
                    equals("system.Octopus.Purpose", "Test")
                    exists("system.Octopus.DotnetSdk3.1")
                }
            })
        }
        yield(DotNetTestBuildType {
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
        yield(DotNetTestBuildType {
            id("Mac OSX".toId(projectName.toId()))
            name = "Mac OSX"
            params {
                param("dotnet_runtime", "osx-x64")
            }
            requirements {
                exists("DotNetCLI")
                equals("teamcity.agent.jvm.os.name", "Mac OS X")
            }
        })
    }

    buildTypesToRegister.forEach { buildType(it.commitStatusPublisher().githubPullRequests()) }
    buildTypesOrder = buildTypes.toList()

    params {
        param("VSTest_TestCaseFilter", "TestCategory != Windows")
        param("dotnet.cli.test.reporting", "off")
    }
})

