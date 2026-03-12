using System;

namespace Calamari.Testing;

//Directly taken from the OctopusDeploy repo
[Flags]
public enum TestPlatforms
{
    Windows = 0b001,
    Linux = 0b010,
    MacOs = 0b100,

    Unix = Linux | MacOs
}