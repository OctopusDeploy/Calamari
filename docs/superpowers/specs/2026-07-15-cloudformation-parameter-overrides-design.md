# CloudFormation direct key/value parameter overrides

## Problem

The "Deploy an AWS CloudFormation template" step always requires stack parameters to come from a file:

- **Package** / **Git repository** template source: a relative path to a JSON parameters file inside the package/repo (`Octopus.Action.Aws.CloudFormationTemplateParametersRaw`, a file path in this context).
- **S3** template source: an S3 URL to a JSON parameters file (`Octopus.Action.Aws.CloudFormationParametersS3URL`), downloaded and parsed at deploy time.
- **Inline** template source is the only exception today: the front end already parses the inline template's declared `Parameters` and renders a typed form per parameter (via `repository.CloudTemplates.getMetadata`), building a JSON array under the hood.

There's no way to just type `Key=Value` parameter pairs directly when the template itself lives in a package, git repo, or S3 — you always need a separate parameters file alongside it.

## Goal

Let users additionally provide parameters as plain key/value pairs, for **all** template sources (Inline, Package, Git repository, and S3), **on top of** whatever the existing mechanism for that source already supplies (typed form for Inline, file for Package/GitRepository, S3 download for S3). Where both are provided, the key/value pairs override matching parameter keys; anything not overridden is unioned in unchanged.

Including Inline isn't strictly necessary — it already has full per-parameter control via the schema-driven form — but it costs nothing extra (Inline already flows through the same Calamari merge point as Package/GitRepository) and keeps the feature consistent and predictable across all four sources. It's also useful when someone pastes in a template authored/parameterized elsewhere and wants to quickly set values without waiting on/trusting the declared-parameter metadata parse.

Not in scope: sensitive/masked value input (follow existing codebase convention — bind to a Sensitive Variable via `#{...}` instead), and schema-aware overrides (no attempt to read the package/git template's declared `Parameters` to drive a typed form — this is a freeform list, like Tags).

## How parameters currently flow (as-is)

```
Front end (React)                          Server                              Calamari (Calamari.Aws)
--------------------                       ------                              -----------------------
CloudFormationTemplateParametersRaw   -->   AwsRunCloudFormationActionHandler   DeployCloudFormationCommand
  (file path, for Package/GitRepo)          -> CloudFormationCalamariPresets    --templateParameters=<path>
                                             .AddTemplateParametersArgument       -> TemplateResolver.MaybeResolve(path, filesInPackage=true)
                                                                                  -> CloudFormationParametersFile.Create(...)
                                                                                  -> JSON.Deserialize<List<Parameter>>

CloudFormationTemplateParameters      -->   (Inline only) builder.WithDataFileAsArgument(   --templateParameters=parameters.json
  (JSON array, for Inline)                   "templateParameters", json, "parameters.json")  -> same resolver, but filesInPackage=false
                                                                                               (file lands in workspace root, not staged package dir)

CloudFormationParametersS3URL         -->   builder.WithArgument("templateS3Parameters", url) --templateS3Parameters=<url>
  (S3 URL)                                                                                     -> CloudFormationS3Template downloads to
                                                                                                    cwd-relative parameters.json, same parser

Octopus.Action.Aws.CloudFormation.Tags -->  (no action-handler wiring at all)                 variables.Get(AwsSpecialVariables.CloudFormation.Tags, "[]")
  (JSON array)                                                                                  read directly inside DeployCloudFormationCommand.Execute
```

Key finding: `RoleArn`, `Tags`, and `IamCapabilities` are read directly off `IVariables` inside `DeployCloudFormationCommand.Execute` (`Calamari.Aws/Commands/DeployAwsCloudFormationCommand.cs:74-75`) with **no** action-handler/`CloudFormationCalamariPresets` involvement — the full step property bag is always available to Calamari as variables, regardless of which CLI args the action handler explicitly wires up. `WithDataFileAsArgument` is only needed when something must land on disk as an actual file (template body, a full parameters file) — not for a plain JSON string. This is the mechanism the new feature will follow, avoiding the file-resolution / staged-package-directory complications entirely.

## Design

### Data model

New property: `Octopus.Action.Aws.CloudFormationTemplateParameterOverrides` — a JSON array of `{ "ParameterKey": "...", "ParameterValue": "..." }` objects (same shape the AWS SDK's `Parameter` type already deserializes to, matching the existing parameters-file format).

### Merge semantics

Primary parameters (from file, for Package/GitRepository; from S3 download, for S3) load first. Overrides are applied on top, keyed by `ParameterKey`: an override replaces a matching key from the primary source, and any override key not present in the primary source is appended. Order of precedence: **overrides win**.

### Calamari changes (`Calamari.Aws`)

1. **`Deployment/AwsSpecialVariables.cs`** — add `TemplateParameterOverrides = "Octopus.Action.Aws.CloudFormationTemplateParameterOverrides"` next to `TemplateParametersRaw`.

2. **`Commands/DeployAwsCloudFormationCommand.cs`** — in `Execute`, read and parse the overrides the same way `Tags`/`IamCapabilities` already are:
   ```csharp
   var parameterOverrides = JsonConvert.DeserializeObject<List<Parameter>>(
       variables.Get(AwsSpecialVariables.CloudFormation.TemplateParameterOverrides, "[]"));
   ```
   No new CLI option, no `WithDataFileAsArgument` — this is a plain variable read, exactly like `Tags`. Pass `parameterOverrides` through to both `CloudFormationTemplate.Create` and `CloudFormationS3Template.Create` (both already reachable from the same `TemplateFactory()` local function used by changeset and non-changeset conventions, so both flows pick this up for free).

3. **Merge point** — add a small static helper, e.g. `CloudFormationParameterMerge.Merge(IEnumerable<Parameter> primary, IEnumerable<Parameter> overrides)` in `Integration/CloudFormation/Templates/`, keyed by `ParameterKey` with overrides winning. Call it from both:
   - `CloudFormationTemplate.Create` (`Integration/CloudFormation/Templates/CloudFormationTemplate.cs:38-68`) — merge `CloudFormationParametersFile.Create(...).Inputs` with `parameterOverrides` before constructing the `CloudFormationTemplate`/passing to `BaseTemplate`.
   - `CloudFormationS3Template.Create` (`Integration/CloudFormation/Templates/CloudFormationS3Template.cs:51-91`) — merge the downloaded-and-parsed `parameters.Inputs` with `parameterOverrides` the same way.

   `BaseTemplate` (`Integration/CloudFormation/Templates/BaseTemplate.cs`) itself doesn't need to change — it just takes the already-merged `IEnumerable<Parameter> inputs` in its constructor, same as today.

4. **No changes needed** to `ApplyCloudFormationChangesetCommand.cs` or `DeleteCloudFormationCommand.cs` — they operate on an already-created changeset/stack by ARN and never touch parameters.

### Server changes (`Octopus.Aws` in the Server repo)

**None required.** `CloudFormationCalamariPresets.cs` and `AwsRunCloudFormationActionHandler.cs` are unchanged — the new property flows to Calamari automatically as part of the standard variable set, the same way `Tags`/`RoleArn`/`IamCapabilities` already do without any action-handler wiring.

(Optional, non-functional: add a matching constant to the server-side `Octopus.Aws/AwsSpecialVariables.cs` purely for consistency/discoverability with the other CloudFormation variable name constants there — nothing in server C# code needs to reference it.)

### Front-end changes (`OctopusDeploy/frontend`)

New file: `frontend/apps/portal/app/components/Actions/aws/awsCloudFormationParameterOverridesSection.tsx`, modeled directly on the existing `AzureBicepParametersSection` (`frontend/apps/portal/app/components/Actions/azure/azureBicepParametersSection.tsx`), which is effectively the same feature already built for Bicep:

```tsx
export interface AwsCloudFormationParameterOverridesSectionProperties {
    "Octopus.Action.Aws.CloudFormationTemplateParameterOverrides": string;
}

const getParameters = (json: string | undefined): KeyValuePair[] => {
    if (!json) return [];
    try { return JSON.parse(json); } catch { return []; }
};

const parametersSummary = (json: string | undefined) => {
    const params = getParameters(json);
    if (params.length === 0) return Summary.placeholder("No additional parameters");
    return Summary.summary(`${params.length} parameter${params.length === 1 ? "" : "s"}`);
};

export const AwsCloudFormationParameterOverridesSection: React.FC<ActionEditProps<AwsCloudFormationParameterOverridesSectionProperties>> = (props) => {
    const { properties, setProperties, localNames, expandedByDefault, projectId, gitRef } = props;
    const propertyKey = "Octopus.Action.Aws.CloudFormationTemplateParameterOverrides";

    return (
        <ExpandableFormSection
            errorKey={propertyKey}
            isExpandedByDefault={expandedByDefault}
            title="Additional Parameters"
            summary={parametersSummary(properties[propertyKey])}
            help="Set or override individual CloudFormation stack parameters directly, without a parameters file"
        >
            <KeyValueEditList
                name="parameter"
                keyLabel="Parameter Key"
                valueLabel="Value"
                items={() => getParameters(properties[propertyKey])}
                onChange={(values) => setProperties({ [propertyKey]: JSON.stringify(values) })}
                projectId={projectId}
                gitRef={gitRef}
                localNames={localNames}
            />
        </ExpandableFormSection>
    );
};
```

Wired into `awsDeployCloudFormationAction.tsx`'s `render()`, always rendered regardless of `TemplateSource`. Placement: alongside `externalTemplateFieldsSection()` for Package/GitRepository, alongside the S3 parameters URL field for S3, and below the existing schema-driven `DynamicForm` (`refreshParametersFromMetadata`/`updateParameters`) for Inline — with help text there noting it overrides the values set above.

Inherited for free from `KeyValueEditList`: duplicate-`ParameterKey` validation, and `#{...}` variable binding on both key and value.

Known gap, addressed by convention rather than new UI: no component in this family (`KeyValueEditList`/`ExtendedKeyValueEditList`) supports masking/sensitive values. The established codebase pattern (Azure ARM `secureString` params, Kubernetes Secrets) is to let users bind a value to an Octopus Sensitive Variable via `#{...}` instead of typing secrets inline, with help text nudging that. Since CloudFormation parameters can be `NoEcho` (e.g. passwords), the `help` text above should mention this.

## Testing

- Calamari: unit tests for the merge helper (override replaces matching key, union of non-overlapping keys, empty overrides is a no-op) and for `DeployCloudFormationCommand`/`CloudFormationS3Template` wiring the overrides variable through. Existing CFN parameter-file tests (e.g. around `CloudFormationParametersFile`) are a good reference for fixture shape.
- Front end: component test for `AwsCloudFormationParameterOverridesSection` (summary text, JSON round-trip through `KeyValueEditList`), and confirm it renders for every `TemplateSource` value, including Inline (where it should override values set by the typed `DynamicForm`).

## Open questions / future considerations (not blocking this design)

- Whether to eventually support array-valued overrides (the Inline flow currently joins array values with `,` per `syncParameters` — see `awsDeployCloudFormationAction.tsx:769-791` — freeform key/value here only supports plain strings, matching the Bicep/Tags precedent).
- Whether a schema-aware version (reading declared parameters out of a package or git ref, like Inline's `DynamicForm`) is ever worth the added server-side work of extracting a template from a package/git ref at edit time.
