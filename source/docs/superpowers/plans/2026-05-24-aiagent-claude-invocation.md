# AiAgent Claude API Invocation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Claude API streaming invocation to the `RunAgentCommand` so it reads a prompt and API token from variables, streams a Claude response (logging chunks in real-time), and sets the full response as an output variable.

**Architecture:** A single `InvokeAgentBehaviour` (implementing `IDeployBehaviour`) uses the official Anthropic C# SDK to stream a Claude Messages API call. Variable names are defined in a `SpecialVariables` class. The behaviour is wired into `RunAgentCommand.Deploy`.

**Tech Stack:** .NET 8.0, Anthropic NuGet SDK (`Anthropic` package), Autofac (via Calamari.Common)

---

## File Structure

**Create:**
- `source/Calamari.AiAgent/SpecialVariables.cs` — variable name constants
- `source/Calamari.AiAgent/Behaviours/InvokeAgentBehaviour.cs` — streams Claude API call

**Modify:**
- `source/Calamari.AiAgent/Calamari.AiAgent.csproj` — add Anthropic NuGet package
- `source/Calamari.AiAgent/RunAgentCommand.cs` — wire InvokeAgentBehaviour into Deploy pipeline

---

### Task 1: Add Anthropic NuGet package

**Files:**
- Modify: `source/Calamari.AiAgent/Calamari.AiAgent.csproj`

- [ ] **Step 1: Add the Anthropic package reference**

In `source/Calamari.AiAgent/Calamari.AiAgent.csproj`, add a `PackageReference` for the official Anthropic SDK inside the existing `ItemGroup` (or a new one). The csproj should become:

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
        <PackageReference Include="Anthropic" Version="12.23.0" />
    </ItemGroup>

    <ItemGroup>
        <ProjectReference Include="..\Calamari.Common\Calamari.Common.csproj" />
    </ItemGroup>

</Project>
```

---

### Task 2: Create SpecialVariables

**Files:**
- Create: `source/Calamari.AiAgent/SpecialVariables.cs`

- [ ] **Step 1: Create SpecialVariables.cs**

Write `source/Calamari.AiAgent/SpecialVariables.cs`:

```csharp
namespace Calamari.AiAgent
{
    static class SpecialVariables
    {
        public static class Action
        {
            public static class AiAgent
            {
                public const string Prompt = "Octopus.Action.AiAgent.Prompt";
                public const string ApiToken = "Octopus.Action.AiAgent.ApiToken";
                public const string Model = "Octopus.Action.AiAgent.Model";
                public const string Response = "Octopus.Action.AiAgent.Response";
            }
        }
    }
}
```

---

### Task 3: Create InvokeAgentBehaviour

**Files:**
- Create: `source/Calamari.AiAgent/Behaviours/InvokeAgentBehaviour.cs`

- [ ] **Step 1: Create the Behaviours directory**

```bash
mkdir -p source/Calamari.AiAgent/Behaviours
```

- [ ] **Step 2: Create InvokeAgentBehaviour.cs**

Write `source/Calamari.AiAgent/Behaviours/InvokeAgentBehaviour.cs`:

```csharp
using System.Text;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AiAgent.Behaviours
{
    public class InvokeAgentBehaviour : IDeployBehaviour
    {
        readonly ILog log;

        public InvokeAgentBehaviour(ILog log)
        {
            this.log = log;
        }

        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public async Task Execute(RunningDeployment context)
        {
            var variables = context.Variables;

            var prompt = variables.Get(SpecialVariables.Action.AiAgent.Prompt);
            if (string.IsNullOrWhiteSpace(prompt))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.Prompt}' is required but was not provided.");

            var apiToken = variables.Get(SpecialVariables.Action.AiAgent.ApiToken);
            if (string.IsNullOrWhiteSpace(apiToken))
                throw new CommandException($"Variable '{SpecialVariables.Action.AiAgent.ApiToken}' is required but was not provided.");

            var model = variables.Get(SpecialVariables.Action.AiAgent.Model);
            if (string.IsNullOrWhiteSpace(model))
                model = "claude-sonnet-4-20250514";

            log.Info($"Invoking AI agent with model '{model}'...");

            var client = new AnthropicClient { ApiKey = apiToken };

            var parameters = new MessageCreateParams
            {
                MaxTokens = 4096,
                Messages =
                [
                    new()
                    {
                        Role = Role.User,
                        Content = prompt,
                    },
                ],
                Model = model,
            };

            var responseBuilder = new StringBuilder();

            await foreach (var streamEvent in client.Messages.CreateStreaming(parameters))
            {
                var text = streamEvent.ToString();
                if (!string.IsNullOrEmpty(text))
                {
                    responseBuilder.Append(text);
                    log.Info(text);
                }
            }

            var fullResponse = responseBuilder.ToString();
            Log.SetOutputVariable(SpecialVariables.Action.AiAgent.Response, fullResponse, variables);
            log.Info("AI agent invocation complete.");
        }
    }
}
```

---

### Task 4: Wire InvokeAgentBehaviour into RunAgentCommand

**Files:**
- Modify: `source/Calamari.AiAgent/RunAgentCommand.cs`

- [ ] **Step 1: Update RunAgentCommand to use InvokeAgentBehaviour**

Replace the contents of `source/Calamari.AiAgent/RunAgentCommand.cs` with:

```csharp
using System.Collections.Generic;
using Calamari.AiAgent.Behaviours;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AiAgent
{
    [Command("run-agent", Description = "Invokes an AI agent")]
    public class RunAgentCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<InvokeAgentBehaviour>();
        }
    }
}
```

---

### Task 5: Verify and commit

- [ ] **Step 1: Verify all files are in place**

Check that these files exist:
- `source/Calamari.AiAgent/SpecialVariables.cs`
- `source/Calamari.AiAgent/Behaviours/InvokeAgentBehaviour.cs`
- `source/Calamari.AiAgent/RunAgentCommand.cs` (updated)
- `source/Calamari.AiAgent/Calamari.AiAgent.csproj` (updated)

- [ ] **Step 2: Verify the wiring test still passes**

Run: `dotnet test source/Calamari.AiAgent.Tests/Calamari.AiAgent.Tests.csproj --filter "FullyQualifiedName~CommandResolutionTests" -v normal`
Expected: 1 test passed. The `InvokeAgentBehaviour` will be resolved via Autofac's assembly scanning in `CalamariFlavourProgramAsync` since it's registered as an `IBehaviour`.

- [ ] **Step 3: Commit**

```bash
git add source/Calamari.AiAgent/
git commit -m "feat: add Claude API streaming invocation to RunAgentCommand"
```
