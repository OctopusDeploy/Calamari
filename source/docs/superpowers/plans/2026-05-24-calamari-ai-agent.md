# Calamari.AiAgent Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a new Calamari flavour project (`Calamari.AiAgent`) for running AI Agent invocations, with a single `run-agent` command and a wiring test.

**Architecture:** Minimal `CalamariFlavourProgramAsync`-based flavour (same pattern as `Calamari.Scripting` and `Calamari.AzureAppService`). One `PipelineCommand` subclass for `run-agent`. A test project validates all commands resolve from the DI container.

**Tech Stack:** .NET 8.0, Autofac (via Calamari.Common), NUnit (tests)

---

## File Structure

**Create:**
- `source/Calamari.AiAgent/Calamari.AiAgent.csproj` — Exe project, net8.0, references Calamari.Common
- `source/Calamari.AiAgent/Program.cs` — Entry point extending `CalamariFlavourProgramAsync`
- `source/Calamari.AiAgent/RunAgentCommand.cs` — `PipelineCommand` with `[Command("run-agent")]`
- `source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj` — Test project
- `source/Calamari.AiAgent.Tests/CommandResolutionTests.cs` — Wiring test

**Modify:**
- `source/Calamari.ConsolidateCalamariPackages/BuildableCalamariProjects.cs:14-24` — Add `"Calamari.AiAgent"` to project list
- `source/Calamari.sln` — Add both new projects (via `dotnet sln add`)

---

### Task 1: Create the Calamari.AiAgent project and Program.cs

**Files:**
- Create: `source/Calamari.AiAgent/Calamari.AiAgent.csproj`
- Create: `source/Calamari.AiAgent/Program.cs`

- [ ] **Step 1: Create the project directory**

```bash
mkdir -p source/Calamari.AiAgent
```

- [ ] **Step 2: Create the .csproj file**

Write `source/Calamari.AiAgent/Calamari.AiAgent.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <AssemblyName>Calamari.AiAgent</AssemblyName>
        <RootNamespace>Calamari.AiAgent</RootNamespace>
        <OutputType>Exe</OutputType>
        <Nullable>enable</Nullable>
        <RuntimeIdentifiers>win-x64;linux-x64;osx-x64;linux-arm;linux-arm64</RuntimeIdentifiers>
        <IsPackable>false</IsPackable>
        <TargetFramework>net8.0</TargetFramework>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>

    <ItemGroup>
        <ProjectReference Include="..\Calamari.Common\Calamari.Common.csproj" />
    </ItemGroup>

</Project>
```

- [ ] **Step 3: Create Program.cs**

Write `source/Calamari.AiAgent/Program.cs`:

```csharp
using System.Threading.Tasks;
using Calamari.Common;
using Calamari.Common.Plumbing.Logging;

namespace Calamari.AiAgent
{
    public class Program : CalamariFlavourProgramAsync
    {
        public Program(ILog log) : base(log)
        {
        }

        public static Task<int> Main(string[] args)
        {
            return new Program(ConsoleLog.Instance).Run(args);
        }
    }
}
```

- [ ] **Step 4: Verify it compiles**

Run: `dotnet build source/Calamari.AiAgent/Calamari.AiAgent.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 2: Create the RunAgentCommand

**Files:**
- Create: `source/Calamari.AiAgent/RunAgentCommand.cs`

- [ ] **Step 1: Create RunAgentCommand.cs**

Write `source/Calamari.AiAgent/RunAgentCommand.cs`:

```csharp
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AiAgent
{
    [Command("run-agent", Description = "Invokes an AI agent")]
    public class RunAgentCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield break;
        }
    }
}
```

- [ ] **Step 2: Verify it compiles**

Run: `dotnet build source/Calamari.AiAgent/Calamari.AiAgent.csproj`
Expected: Build succeeded with 0 errors.

---

### Task 3: Create the test project with wiring test

**Files:**
- Create: `source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj`
- Create: `source/Calamari.AiAgent.Tests/CommandResolutionTests.cs`

- [ ] **Step 1: Create the test project directory**

```bash
mkdir -p source/Calamari.AiAgent.Tests
```

- [ ] **Step 2: Create the test .csproj file**

Write `source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <AssemblyName>Calamari.AiAgent.Tests</AssemblyName>
        <RootNamespace>Calamari.AiAgent.Tests</RootNamespace>
        <TargetFramework>net8.0</TargetFramework>
        <RuntimeIdentifiers>win-x64;linux-x64;osx-x64;linux-arm;linux-arm64</RuntimeIdentifiers>
        <IsPackable>false</IsPackable>
        <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    </PropertyGroup>
    <ItemGroup>
        <PackageReference Include="FluentAssertions" Version="7.2.0" />
        <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.0" />
        <PackageReference Include="NSubstitute" Version="4.2.2" />
        <PackageReference Include="nunit" Version="3.14.0" />
        <PackageReference Include="NUnit3TestAdapter" Version="5.2.0" />
        <PackageReference Include="TeamCity.VSTest.TestAdapter" Version="1.0.41" />
    </ItemGroup>
    <ItemGroup>
        <ProjectReference Include="..\Calamari.AiAgent\Calamari.AiAgent.csproj" />
        <ProjectReference Include="..\Calamari.Testing\Calamari.Testing.csproj" />
    </ItemGroup>
</Project>
```

- [ ] **Step 3: Create the wiring test**

Write `source/Calamari.AiAgent.Tests/CommandResolutionTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using Autofac;
using Calamari.Testing;
using NUnit.Framework;

namespace Calamari.AiAgent.Tests;

[TestFixture]
public class CommandResolutionTests
{
    [Test]
    [Category("PlatformAgnostic")]
    public void AllPipelineCommandsCanBeConstructed()
    {
        var program = TestablePipelineProgram.For<Calamari.AiAgent.Program>();
        using var container = program.BuildTestContainer();

        var failures = new List<string>();
        foreach (var type in program.PipelineCommandTypes)
        {
            try
            {
                container.Resolve(type);
            }
            catch (Exception ex)
            {
                failures.Add($"'{type.Name}': {ex.Message}");
            }
        }

        Assert.That(failures, Is.Empty, "all pipeline commands must be constructable from the DI container");
    }
}
```

- [ ] **Step 4: Verify test project compiles**

Run: `dotnet build source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj`
Expected: Build succeeded with 0 errors.

- [ ] **Step 5: Run the test**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~CommandResolutionTests" -v normal`
Expected: 1 test passed.

---

### Task 4: Add projects to solution and consolidation list

**Files:**
- Modify: `source/Calamari.sln`
- Modify: `source/Calamari.ConsolidateCalamariPackages/BuildableCalamariProjects.cs`

- [ ] **Step 1: Add both projects to the solution**

```bash
cd source && dotnet sln Calamari.sln add Calamari.AiAgent/Calamari.AiAgent.csproj Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj
```

Expected: Two "Project added to the solution" messages.

- [ ] **Step 2: Add to BuildableCalamariProjects.cs**

In `source/Calamari.ConsolidateCalamariPackages/BuildableCalamariProjects.cs`, add `"Calamari.AiAgent"` to the `NonWindows` array (which is also included in the `Windows` array). The array should become:

```csharp
static readonly string[] NonWindows =
[
    "Calamari",
    "Calamari.AiAgent",
    "Calamari.AzureAppService",
    "Calamari.AzureResourceGroup",
    "Calamari.GoogleCloudScripting",
    "Calamari.AzureScripting",
    "Calamari.Terraform"
];
```

- [ ] **Step 3: Verify the full solution builds**

Run: `dotnet build source/Calamari.sln`
Expected: Build succeeded with 0 errors.

- [ ] **Step 4: Run the wiring test one final time**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~CommandResolutionTests" -v normal`
Expected: 1 test passed.

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.AiAgent/ source/Calamari.AiAgent.Tests/ source/Calamari.sln source/Calamari.ConsolidateCalamariPackages/BuildableCalamariProjects.cs
git commit -m "feat: add Calamari.AiAgent project for AI agent invocations"
```
