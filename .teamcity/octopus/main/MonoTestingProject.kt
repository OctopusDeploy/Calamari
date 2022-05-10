package octopus.main

import jetbrains.buildServer.configs.kotlin.v2019_2.Project
import jetbrains.buildServer.configs.kotlin.v2019_2.toId
import octopus.main.buildtypes.MonoTestBuildType

class MonoTestingProject : Project({
    val projectName = "Mono Testing"
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
            yield(MonoTestBuildType {
                id(item.second.toId(projectName.toId()))
                name = item.first
                params {
                    param("VSTest_TestCaseFilter", "TestCategory != Windows & TestCategory != MacOS")
                    param("NUnit_TestCaseFilter", "cat != Windows && cat != macOS")
                }
                requirements {
                    equals("system.Octopus.OS", item.second)
                    equals("system.Octopus.Purpose", "Test")
                    exists("MonoVersion")
                }
            })
        }
        yield(MonoTestBuildType {
            id("Mac OSX".toId(projectName.toId()))
            name = "Mac OSX"
            params {
                param("MonoBin", "/Library/Frameworks/Mono.framework/Versions/Current/Commands/mono")
                param("VSTest_TestCaseFilter", "TestCategory != Windows & TestCategory != Nix")
                param("NUnit_TestCaseFilter", "cat != Windows && cat != Nix")
            }
            requirements {
                equals("system.MonoVersion", "5.14.0")
                equals("teamcity.agent.jvm.os.name", "Mac OS X")
                exists("MonoVersion")
            }
        })
    }

    buildTypesToRegister.forEach { buildType(it.commitStatusPublisher().githubPullRequests()) }
    buildTypesOrder = buildTypes.toList()

    params {
        param("MonoBin", "/usr/bin/mono")
        param("NUnitBin", "/opt/NUnit/NUnit-3.4.1/bin/nunit3-console.exe")
        param("NUnitExcludes", "Windows,macOS")
        param("dotnet_runtime", "netfx")
    }
})
