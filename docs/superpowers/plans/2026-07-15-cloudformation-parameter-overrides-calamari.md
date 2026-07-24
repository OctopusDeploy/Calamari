# CloudFormation Parameter Overrides (Calamari) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let CloudFormation stack parameters supplied directly as key/value pairs (via a new `Octopus.Action.Aws.CloudFormationTemplateParameterOverrides` variable) override/extend whatever parameters already come from a parameters file (Package/Git/Inline) or S3 download.

**Architecture:** Read the new variable directly off `IVariables` inside `DeployCloudFormationCommand.Execute` — no new CLI option and no `WithDataFileAsArgument` bundling, mirroring how `Tags`/`IamCapabilities` are already read directly today. Merge the parsed overrides with the primary parameter source (file-based or S3-based) via a new pure `CloudFormationParameterMerge.Merge` helper, called from both `CloudFormationTemplate.Create` and `CloudFormationS3Template.Create` before constructing `BaseTemplate`. Overrides win on matching `ParameterKey`; everything else is unioned in unchanged.

**Tech Stack:** C#/.NET, NUnit + FluentAssertions + NSubstitute (existing `Calamari.Tests` conventions), `Amazon.CloudFormation.Model.Parameter` (AWS SDK), `Newtonsoft.Json`.

## Global Constraints

- Spec: `docs/superpowers/specs/2026-07-15-cloudformation-parameter-overrides-design.md`.
- Variable name: `Octopus.Action.Aws.CloudFormationTemplateParameterOverrides`, exposed as `AwsSpecialVariables.CloudFormation.TemplateParameterOverrides`.
- Format: JSON array of `{ "ParameterKey": "...", "ParameterValue": "..." }` objects — same shape `CloudFormationParametersFile` already deserializes.
- Merge rule: overrides replace a primary-source parameter with the same `ParameterKey`; parameters only present in overrides are appended; parameters only present in the primary source pass through unchanged.
- No CLI option, no bundled data file — the variable is read directly via `variables.Get(...)`, same as `AwsSpecialVariables.CloudFormation.Tags` at `Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs:75`.
- `ApplyCloudFormationChangesetCommand.cs` and `DeleteCloudFormationCommand.cs` are out of scope — they operate on an already-created changeset/stack and never touch parameters.
- Tests live in `Calamari.Tests/AWS/CloudFormation/`, namespace `Calamari.Tests.AWS.CloudFormation`, NUnit `[TestFixture]`/`[Test]`, FluentAssertions `.Should()`, NSubstitute `Substitute.For<T>()`, tagged `[Category(TestCategory.PlatformAgnostic)]` (fast unit tests, no real AWS calls) — following `Calamari.Tests/AWS/CloudFormation/WaitForStackToCompleteTests.cs` as the current idiom (file-scoped namespace).
- Test project: `Calamari.Tests.csproj` already references `Calamari.Aws.csproj` — no new project references needed.

---

### Task 1: `CloudFormationParameterMerge` helper

**Files:**
- Create: `Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationParameterMerge.cs`
- Test: `Calamari.Tests/AWS/CloudFormation/CloudFormationParameterMergeFixture.cs`

**Interfaces:**
- Produces: `public static class CloudFormationParameterMerge { public static List<Parameter> Merge(IEnumerable<Parameter> primary, IEnumerable<Parameter> overrides) }` — used by Task 2 and Task 3.

- [ ] **Step 1: Write the failing tests**

Create `Calamari.Tests/AWS/CloudFormation/CloudFormationParameterMergeFixture.cs`:

```csharp
using System.Collections.Generic;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation.Templates;
using FluentAssertions;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class CloudFormationParameterMergeFixture
{
    [Test]
    public void Merge_OverrideReplacesMatchingKey()
    {
        var primary = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } };
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } };

        var result = CloudFormationParameterMerge.Merge(primary, overrides);

        result.Should().BeEquivalentTo(new[] { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } });
    }

    [Test]
    public void Merge_OverrideAppendsNewKey()
    {
        var primary = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } };
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Bar", ParameterValue = "New" } };

        var result = CloudFormationParameterMerge.Merge(primary, overrides);

        result.Should().BeEquivalentTo(new[]
        {
            new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" },
            new Parameter { ParameterKey = "Bar", ParameterValue = "New" }
        });
    }

    [Test]
    public void Merge_EmptyOverrides_ReturnsPrimaryUnchanged()
    {
        var primary = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } };

        var result = CloudFormationParameterMerge.Merge(primary, new List<Parameter>());

        result.Should().BeEquivalentTo(primary);
    }

    [Test]
    public void Merge_EmptyPrimary_ReturnsOverridesOnly()
    {
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } };

        var result = CloudFormationParameterMerge.Merge(new List<Parameter>(), overrides);

        result.Should().BeEquivalentTo(overrides);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~CloudFormationParameterMergeFixture"`
Expected: FAIL to build — `CloudFormationParameterMerge` does not exist.

- [ ] **Step 3: Write the implementation**

Create `Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationParameterMerge.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using Amazon.CloudFormation.Model;

namespace Calamari.Aws.Integration.CloudFormation.Templates
{
    public static class CloudFormationParameterMerge
    {
        public static List<Parameter> Merge(IEnumerable<Parameter> primary, IEnumerable<Parameter> overrides)
        {
            var merged = new Dictionary<string, Parameter>();

            foreach (var parameter in primary)
                merged[parameter.ParameterKey] = parameter;

            foreach (var parameter in overrides)
                merged[parameter.ParameterKey] = parameter;

            return merged.Values.ToList();
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~CloudFormationParameterMergeFixture"`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add source/Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationParameterMerge.cs source/Calamari.Tests/AWS/CloudFormation/CloudFormationParameterMergeFixture.cs
git commit -m "Add CloudFormationParameterMerge helper for merging parameter overrides"
```

---

### Task 2: Wire overrides into `CloudFormationTemplate.Create` and `DeployCloudFormationCommand`

**Files:**
- Modify: `Calamari.Aws/Deployment/AwsSpecialVariables.cs`
- Modify: `Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationTemplate.cs`
- Modify: `Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs`
- Test: `Calamari.Tests/AWS/CloudFormation/CloudFormationTemplateFixture.cs`

**Interfaces:**
- Consumes: `CloudFormationParameterMerge.Merge(IEnumerable<Parameter>, IEnumerable<Parameter>)` from Task 1.
- Produces: `CloudFormationTemplate.Create(...)` gains a new `IEnumerable<Parameter> parameterOverrides` parameter (inserted immediately after `templateParameterFile`); `AwsSpecialVariables.CloudFormation.TemplateParameterOverrides` constant, used by Task 3 and by the Server-side change (out of scope for this plan).

- [ ] **Step 1: Write the failing test**

Create `Calamari.Tests/AWS/CloudFormation/CloudFormationTemplateFixture.cs`:

```csharp
using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;
using Octopus.CoreUtilities;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class CloudFormationTemplateFixture
{
    [Test]
    public void Create_MergesParameterOverridesOverFileParameters()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        fileSystem.ReadFile("resolved/template.yaml").Returns("template content");
        fileSystem.ReadFile("resolved/parameters.json")
                   .Returns("[{\"ParameterKey\":\"Foo\",\"ParameterValue\":\"FromFile\"},{\"ParameterKey\":\"Bar\",\"ParameterValue\":\"FromFile\"}]");

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.Resolve("template.yaml", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/template.yaml"));
        templateResolver.MaybeResolve("parameters.json", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/parameters.json").AsSome());

        var variables = new CalamariVariables();
        var overrides = new List<Parameter>
        {
            new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" },
            new Parameter { ParameterKey = "Baz", ParameterValue = "New" }
        };

        var builder = CloudFormationTemplate.Create(templateResolver,
                                                     "template.yaml",
                                                     "parameters.json",
                                                     overrides,
                                                     false,
                                                     fileSystem,
                                                     variables,
                                                     "my-stack",
                                                     new List<string>(),
                                                     false,
                                                     null,
                                                     new List<KeyValuePair<string, string>>(),
                                                     new StackArn("my-stack"),
                                                     () => Substitute.For<IAmazonCloudFormation>());

        builder.Inputs.Should().BeEquivalentTo(new[]
        {
            new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" },
            new Parameter { ParameterKey = "Bar", ParameterValue = "FromFile" },
            new Parameter { ParameterKey = "Baz", ParameterValue = "New" }
        });
    }

    [Test]
    public void Create_WithNoOverrides_ReturnsFileParametersUnchanged()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        fileSystem.ReadFile("resolved/template.yaml").Returns("template content");
        fileSystem.ReadFile("resolved/parameters.json")
                   .Returns("[{\"ParameterKey\":\"Foo\",\"ParameterValue\":\"FromFile\"}]");

        var templateResolver = Substitute.For<ITemplateResolver>();
        templateResolver.Resolve("template.yaml", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/template.yaml"));
        templateResolver.MaybeResolve("parameters.json", false, Arg.Any<IVariables>())
                         .Returns(new ResolvedTemplatePath("resolved/parameters.json").AsSome());

        var variables = new CalamariVariables();

        var builder = CloudFormationTemplate.Create(templateResolver,
                                                     "template.yaml",
                                                     "parameters.json",
                                                     new List<Parameter>(),
                                                     false,
                                                     fileSystem,
                                                     variables,
                                                     "my-stack",
                                                     new List<string>(),
                                                     false,
                                                     null,
                                                     new List<KeyValuePair<string, string>>(),
                                                     new StackArn("my-stack"),
                                                     () => Substitute.For<IAmazonCloudFormation>());

        builder.Inputs.Should().BeEquivalentTo(new[] { new Parameter { ParameterKey = "Foo", ParameterValue = "FromFile" } });
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~CloudFormationTemplateFixture"`
Expected: FAIL to build — `CloudFormationTemplate.Create` doesn't accept a `parameterOverrides` argument yet.

- [ ] **Step 3: Add the variable constant**

In `Calamari.Aws/Deployment/AwsSpecialVariables.cs`, inside `public static class CloudFormation`, change:

```csharp
public const string TemplateParametersRaw = "Octopus.Action.Aws.CloudFormationTemplateParametersRaw";
```

to:

```csharp
public const string TemplateParametersRaw = "Octopus.Action.Aws.CloudFormationTemplateParametersRaw";
public const string TemplateParameterOverrides = "Octopus.Action.Aws.CloudFormationTemplateParameterOverrides";
```

- [ ] **Step 4: Update `CloudFormationTemplate` to accept and merge overrides**

Replace the full contents of `Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationTemplate.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Deployment;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Octopus.CoreUtilities;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation.Templates;

public class CloudFormationTemplate(
    Func<string> content,
    IEnumerable<Parameter> inputs,
    string stackName,
    List<string> iamCapabilities,
    bool disableRollback,
    string roleArn,
    IEnumerable<KeyValuePair<string, string>> tags,
    StackArn stack,
    Func<IAmazonCloudFormation> clientFactory,
    IVariables variables)
    : BaseTemplate(inputs,
                   stackName,
                   iamCapabilities,
                   disableRollback,
                   roleArn,
                   tags,
                   stack,
                   clientFactory,
                   variables), ITemplate
{
    public static ICloudFormationRequestBuilder Create(ITemplateResolver templateResolver,
                                                       string templateFile,
                                                       string templateParameterFile,
                                                       IEnumerable<Parameter> parameterOverrides,
                                                       bool filesInPackage,
                                                       ICalamariFileSystem fileSystem,
                                                       IVariables variables,
                                                       string stackName,
                                                       List<string> capabilities,
                                                       bool disableRollback,
                                                       string roleArn,
                                                       IEnumerable<KeyValuePair<string, string>> tags,
                                                       StackArn stack,
                                                       Func<IAmazonCloudFormation> clientFactory)
    {
        var resolvedTemplate = templateResolver.Resolve(templateFile, filesInPackage, variables);
        var resolvedParameters = templateResolver.MaybeResolve(templateParameterFile, filesInPackage, variables);

        if (!string.IsNullOrWhiteSpace(templateParameterFile) && !resolvedParameters.Some())
            throw new CommandException("Could not find template parameters file: " + templateParameterFile);

        var primaryInputs = CloudFormationParametersFile.Create(resolvedParameters, fileSystem, variables).Inputs;
        var mergedInputs = CloudFormationParameterMerge.Merge(primaryInputs, parameterOverrides);

        return new CloudFormationTemplate(() => variables.Evaluate(fileSystem.ReadFile(resolvedTemplate.Value)),
                                          mergedInputs,
                                          stackName,
                                          capabilities,
                                          disableRollback,
                                          roleArn,
                                          tags,
                                          stack,
                                          clientFactory,
                                          variables);
    }

    public string Content => content();

    public override CreateStackRequest BuildCreateStackRequest()
    {
        return new CreateStackRequest
        {
            StackName = stackName,
            TemplateBody = Content,
            Parameters = Inputs.ToList(),
            Capabilities = capabilities,
            DisableRollback = disableRollback,
            RoleARN = roleArn,
            Tags = tags
        };
    }

    public override UpdateStackRequest BuildUpdateStackRequest()
    {
        return new UpdateStackRequest
        {
            StackName = stackName,
            TemplateBody = Content,
            Parameters = Inputs.ToList(),
            Capabilities = capabilities,
            RoleARN = roleArn,
            Tags = tags
        };
    }

    public override async Task<CreateChangeSetRequest> BuildChangesetRequest()
    {
        return new CreateChangeSetRequest
        {
            StackName = stack.Value,
            TemplateBody = Content,
            Parameters = Inputs.ToList(),
            /*
             * The change set name might be passed down directly, or this variable may be
             * set as part of the deployment. Reading the value from the variables here
             * allows us to catch any deferred construction of the change stack name.
             */
            ChangeSetName = variables[AwsSpecialVariables.CloudFormation.Changesets.Name],
            ChangeSetType = await GetStackStatus() == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
            Capabilities = capabilities,
            RoleARN = roleArn,
            Tags = tags
        };
    }
}
```

(Only the primary constructor's second parameter, `Create`'s signature, and the body computing `mergedInputs` have changed from the original file — everything else is unchanged.)

- [ ] **Step 5: Update `DeployCloudFormationCommand` to read and pass through the overrides variable**

In `Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs`:

Add `using Amazon.CloudFormation.Model;` to the top of the file (alongside the existing `using Amazon.CloudFormation;`).

Change:

```csharp
var iamCapabilities = JsonConvert.DeserializeObject<List<string>>(variables.Get(AwsSpecialVariables.IamCapabilities, "[]"));
var tags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags, "[]"));
```

to:

```csharp
var iamCapabilities = JsonConvert.DeserializeObject<List<string>>(variables.Get(AwsSpecialVariables.IamCapabilities, "[]"));
var tags = JsonConvert.DeserializeObject<List<KeyValuePair<string, string>>>(variables.Get(AwsSpecialVariables.CloudFormation.Tags, "[]"));
var parameterOverrides = JsonConvert.DeserializeObject<List<Parameter>>(variables.Get(AwsSpecialVariables.CloudFormation.TemplateParameterOverrides, "[]"));
```

Change the `templateFile` branch of `TemplateFactory` from:

```csharp
ICloudFormationRequestBuilder TemplateFactory() => string.IsNullOrWhiteSpace(templateS3Url)
    ? CloudFormationTemplate.Create(templateResolver,
                                    templateFile,
                                    templateParameterFile,
                                    filesInPackage,
                                    fileSystem,
                                    variables,
                                    stackName,
                                    iamCapabilities,
                                    disableRollback,
                                    RoleArnProvider(deployment),
                                    tags,
                                    StackProvider(deployment),
                                    ClientFactory)
```

to:

```csharp
ICloudFormationRequestBuilder TemplateFactory() => string.IsNullOrWhiteSpace(templateS3Url)
    ? CloudFormationTemplate.Create(templateResolver,
                                    templateFile,
                                    templateParameterFile,
                                    parameterOverrides,
                                    filesInPackage,
                                    fileSystem,
                                    variables,
                                    stackName,
                                    iamCapabilities,
                                    disableRollback,
                                    RoleArnProvider(deployment),
                                    tags,
                                    StackProvider(deployment),
                                    ClientFactory)
```

(The `CloudFormationS3Template.Create` branch is updated in Task 3.)

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~CloudFormationTemplateFixture"`
Expected: PASS (2 tests). `CloudFormationS3Template.Create`'s call site in the same file is untouched by this task and keeps compiling against its existing signature — Task 3 updates it.

- [ ] **Step 7: Commit**

```bash
git add source/Calamari.Aws/Deployment/AwsSpecialVariables.cs source/Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationTemplate.cs source/Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs source/Calamari.Tests/AWS/CloudFormation/CloudFormationTemplateFixture.cs
git commit -m "Merge CloudFormation parameter overrides into file-based template parameters"
```

---

### Task 3: Wire overrides into `CloudFormationS3Template.Create`

**Files:**
- Modify: `Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationS3Template.cs`
- Modify: `Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs`
- Test: `Calamari.Tests/AWS/CloudFormation/CloudFormationS3TemplateFixture.cs`

**Interfaces:**
- Consumes: `CloudFormationParameterMerge.Merge(...)` from Task 1; `AwsSpecialVariables.CloudFormation.TemplateParameterOverrides` and the `parameterOverrides` local from Task 2.
- Produces: `CloudFormationS3Template.Create(...)` gains a new `IEnumerable<Parameter> parameterOverrides` parameter (inserted immediately after `templateParameterS3Url`).

- [ ] **Step 1: Write the failing test**

Create `Calamari.Tests/AWS/CloudFormation/CloudFormationS3TemplateFixture.cs`:

```csharp
using System.Collections.Generic;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Calamari.Aws.Integration.CloudFormation.Templates;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using FluentAssertions;
using NSubstitute;
using NUnit.Framework;

namespace Calamari.Tests.AWS.CloudFormation;

[TestFixture]
public class CloudFormationS3TemplateFixture
{
    [Test]
    public void Create_WithNoParametersS3Url_UsesOverridesOnly()
    {
        var fileSystem = Substitute.For<ICalamariFileSystem>();
        var variables = new CalamariVariables();
        var log = Substitute.For<ILog>();
        var overrides = new List<Parameter> { new Parameter { ParameterKey = "Foo", ParameterValue = "Overridden" } };

        var builder = CloudFormationS3Template.Create("https://example.s3.amazonaws.com/template.yaml",
                                                       null,
                                                       overrides,
                                                       fileSystem,
                                                       variables,
                                                       log,
                                                       "my-stack",
                                                       new List<string>(),
                                                       false,
                                                       null,
                                                       new List<KeyValuePair<string, string>>(),
                                                       new StackArn("my-stack"),
                                                       () => Substitute.For<IAmazonCloudFormation>());

        builder.Inputs.Should().BeEquivalentTo(overrides);
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~CloudFormationS3TemplateFixture"`
Expected: FAIL to build — `CloudFormationS3Template.Create` doesn't accept a `parameterOverrides` argument yet.

- [ ] **Step 3: Update `CloudFormationS3Template` to accept and merge overrides**

Replace the full contents of `Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationS3Template.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.CloudFormation;
using Amazon.CloudFormation.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Calamari.Aws.Deployment;
using Calamari.Aws.Util;
using Calamari.CloudAccounts;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Common.Util;
using Octopus.CoreUtilities;
using StackStatus = Calamari.Aws.Deployment.Conventions.StackStatus;

namespace Calamari.Aws.Integration.CloudFormation.Templates;

public class CloudFormationS3Template : BaseTemplate
{
    const string ParametersFile = "parameters.json";
    readonly string templateS3Url;

    CloudFormationS3Template(IEnumerable<Parameter> inputs,
                             string templateS3Url,
                             string stackName,
                             List<string> iamCapabilities,
                             bool disableRollback,
                             string roleArn,
                             IEnumerable<KeyValuePair<string, string>> tags,
                             StackArn stack,
                             Func<IAmazonCloudFormation> clientFactory,
                             IVariables variables) : base(inputs,
                                                          stackName,
                                                          iamCapabilities,
                                                          disableRollback,
                                                          roleArn,
                                                          tags,
                                                          stack,
                                                          clientFactory,
                                                          variables)
    {
        this.templateS3Url = templateS3Url;
    }

    public static ICloudFormationRequestBuilder Create(string templateS3Url,
                                                       string templateParameterS3Url,
                                                       IEnumerable<Parameter> parameterOverrides,
                                                       ICalamariFileSystem fileSystem,
                                                       IVariables variables,
                                                       ILog log,
                                                       string stackName,
                                                       List<string> capabilities,
                                                       bool disableRollback,
                                                       string roleArn,
                                                       IEnumerable<KeyValuePair<string, string>> tags,
                                                       StackArn stack,
                                                       Func<IAmazonCloudFormation> clientFactory)
    {
        if (!string.IsNullOrWhiteSpace(templateParameterS3Url) && !templateParameterS3Url.StartsWith("http"))
            throw new CommandException("Parameters file must start with http: " + templateParameterS3Url);

        if (!string.IsNullOrWhiteSpace(templateS3Url) && !templateS3Url.StartsWith("http"))
            throw new CommandException("Template file must start with http: " + templateS3Url);

        var templatePath = string.IsNullOrWhiteSpace(templateParameterS3Url)
            ? Maybe<ResolvedTemplatePath>.None
            : new ResolvedTemplatePath(ParametersFile).AsSome();

        if (templatePath.Some())
        {
            DownloadS3(variables, log, templateParameterS3Url);
        }

        var parameters = CloudFormationParametersFile.Create(templatePath, fileSystem, variables);
        var mergedInputs = CloudFormationParameterMerge.Merge(parameters.Inputs, parameterOverrides);

        return new CloudFormationS3Template(mergedInputs,
                                            templateS3Url,
                                            stackName,
                                            capabilities,
                                            disableRollback,
                                            roleArn,
                                            tags,
                                            stack,
                                            clientFactory,
                                            variables);
    }

    /// <summary>
    /// The SDK allows us to deploy a template from a URL, but does not apply parameters from a URL. So we
    /// must download the parameters file and parse it locally.
    /// </summary>
    static void DownloadS3(IVariables variables, ILog log, string templateParameterS3Url)
    {
        try
        {
            var environment = AwsEnvironmentGeneration.Create(log, variables).GetAwaiter().GetResult();
            var s3Uri = new AmazonS3Uri(templateParameterS3Url);
            using IAmazonS3 client = ClientHelpers.CreateS3Client(environment);
            var request = new GetObjectRequest
            {
                BucketName = s3Uri.Bucket,
                Key = s3Uri.Key
            };
            var response = client.GetObjectAsync(request).GetAwaiter().GetResult();
            response.WriteResponseStreamToFileAsync(ParametersFile, false, new CancellationTokenSource().Token).GetAwaiter().GetResult();
        }
        catch (UriFormatException)
        {
            log.Error($"The parameters URL of {templateParameterS3Url} is invalid");
            throw;
        }
    }

    public override CreateStackRequest BuildCreateStackRequest()
    {
        return new CreateStackRequest
        {
            StackName = stackName,
            TemplateURL = templateS3Url,
            Parameters = Inputs.ToList(),
            Capabilities = capabilities,
            DisableRollback = disableRollback,
            RoleARN = roleArn,
            Tags = tags
        };
    }

    public override UpdateStackRequest BuildUpdateStackRequest()
    {
        return new UpdateStackRequest
        {
            StackName = stackName,
            TemplateURL = templateS3Url,
            Parameters = Inputs.ToList(),
            Capabilities = capabilities,
            RoleARN = roleArn,
            Tags = tags
        };
    }

    public override async Task<CreateChangeSetRequest> BuildChangesetRequest()
    {
        return new CreateChangeSetRequest
        {
            StackName = stack.Value,
            TemplateURL = templateS3Url,
            Parameters = Inputs.ToList(),
            ChangeSetName = variables[AwsSpecialVariables.CloudFormation.Changesets.Name],
            ChangeSetType = await GetStackStatus() == StackStatus.DoesNotExist ? ChangeSetType.CREATE : ChangeSetType.UPDATE,
            Capabilities = capabilities,
            RoleARN = roleArn,
            Tags = tags
        };
    }
}
```

(Only the constructor's first parameter, `Create`'s signature, and the body computing `mergedInputs` have changed from the original file — everything else is unchanged.)

- [ ] **Step 4: Update `DeployCloudFormationCommand`'s S3 branch**

In `Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs`, change the `templateS3Url` branch of `TemplateFactory` from:

```csharp
: CloudFormationS3Template.Create(templateS3Url,
                                  templateParameterS3Url,
                                  fileSystem,
                                  variables,
                                  log,
                                  stackName,
                                  iamCapabilities,
                                  disableRollback,
                                  RoleArnProvider(deployment),
                                  tags,
                                  StackProvider(deployment),
                                  ClientFactory);
```

to:

```csharp
: CloudFormationS3Template.Create(templateS3Url,
                                  templateParameterS3Url,
                                  parameterOverrides,
                                  fileSystem,
                                  variables,
                                  log,
                                  stackName,
                                  iamCapabilities,
                                  disableRollback,
                                  RoleArnProvider(deployment),
                                  tags,
                                  StackProvider(deployment),
                                  ClientFactory);
```

- [ ] **Step 5: Run all CloudFormation unit tests to verify everything passes**

Run: `dotnet test source/Calamari.Tests/Calamari.Tests.csproj --filter "FullyQualifiedName~Calamari.Tests.AWS.CloudFormation & Category=PlatformAgnostic"`
Expected: PASS — all of `CloudFormationParameterMergeFixture`, `CloudFormationTemplateFixture`, `CloudFormationS3TemplateFixture`, plus the pre-existing `CloudFormationObjectExtensionsFixture`/`WaitForStackToCompleteTests`.

- [ ] **Step 6: Build the full solution to confirm nothing else references the old signatures**

Run: `dotnet build source/Calamari.sln`
Expected: Build succeeds with no errors (the `Calamari.Aws`/`Calamari.Tests` projects are the only consumers of `CloudFormationTemplate.Create`/`CloudFormationS3Template.Create`).

- [ ] **Step 7: Commit**

```bash
git add source/Calamari.Aws/Integration/CloudFormation/Templates/CloudFormationS3Template.cs source/Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs source/Calamari.Tests/AWS/CloudFormation/CloudFormationS3TemplateFixture.cs
git commit -m "Merge CloudFormation parameter overrides into S3-based template parameters"
```

---

## Notes for the next plan (front-end, separate repo)

This plan makes Calamari read `Octopus.Action.Aws.CloudFormationTemplateParameterOverrides` automatically once it's present in the variable set — no Server/action-handler change is required (see spec). The front-end plan (in `OctopusDeploy/frontend`) only needs to add the UI that writes that property; it has no compile-time dependency on this repo and can ship independently in either order.
