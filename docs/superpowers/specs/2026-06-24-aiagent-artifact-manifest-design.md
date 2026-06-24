# AiAgent Artifact Manifest — Design

**Date:** 2026-06-24
**Status:** Approved (design)
**Component:** `Calamari.AiAgent` — Claude Code behaviour

## Problem

The AiAgent step invokes the Claude Code CLI to perform work during an Octopus
deployment. Users will sometimes ask the agent to produce output — *"generate a
file with today's date"*, *"analyse this data and create a csv"*, *"create files
that…"*, *"create a website that…"* — and want that output surfaced as an Octopus
**artifact** for later acquisition. This ranges from a single file, to several
discrete files, to a whole directory tree (e.g. a generated website). Octopus
collects
artifacts via the `NewOctopusArtifact` service message
(`ILog.NewOctopusArtifact(fullPath, name, fileLength)`).

We need a predictable mechanism to flag agent-created files as artifacts.

### Constraints discovered in the existing code

1. The agent's working directory is a `TemporaryDirectory` that is **deleted**
   when `InvokeClaudeCodeBehaviour.Execute` returns (`using var tempDir`). Any
   file the agent creates in `workingDir` is gone once the step ends.
2. The established pattern already handles this: for the debug log,
   `ClaudeCodeCliRunner.RunAsync` (lines ~52–58) **moves the file out of the temp
   dir into `calamariDir`** and only *then* emits `NewOctopusArtifact`, all before
   the temp dir disposes. Artifact capture is Calamari-managed and deterministic.
3. The CLI process runs with `workingDir` as its current directory, so agent
   `Write` calls with relative paths land under `workingDir`. Sandbox modes
   (Bash/SRT) further constrain writes toward the working dir.

## Approaches considered

- **Auto-scrape the working dir** at end of run — rejected: unpredictable scope,
  picks up unintended files.
- **Watch file-writing tool invocations** — rejected: doesn't cover all tools,
  still unpredictable scope.
- **Skill telling Claude to always emit a service message on file creation** —
  rejected: same scope problem, plus fragility.
- **Explicit intent + Calamari-managed capture** — chosen. The user must
  explicitly ask for an artifact; Calamari deterministically performs the capture.

Within the chosen approach, two declaration surfaces were weighed:

- **Manifest file (chosen for v1):** the agent records declarations in a file;
  Calamari reads it after the run. No new dependencies.
- **In-process MCP tool (deferred):** a `create_octopus_artifact` tool hosted
  in-process over HTTP. Nicer surface (typed args, immediate validation feedback
  to the model) but requires pulling an HTTP host + the MCP SDK into Calamari and
  owning port/lifecycle. Revisit if the manifest proves unreliable.

Why **skill** over slash **command** as the trigger: the artifact's name/path is
almost always determined *during* the run (the agent generates the file), not when
the step prompt is authored. A skill triggers semantically on natural-language
intent; a command would force the author to know the filename up front.

## Decision

Skill (always on) → manifest file → Calamari validates, copies out of the temp
dir, emits `NewOctopusArtifact`. Working-dir-only boundary. Hard-fail on invalid
entries. A manifest entry may reference a **single file** or a **directory**;
multiple files are captured via multiple entries, and a directory is zipped into a
single bundle artifact. No new SpecialVariables, no MCP, no new dependencies.

## Design

### Components

- **`octopus-artifacts` skill** — new embedded resource under
  `Calamari.AiAgent/ClaudeCodeBehaviour/DefaultContext/Skills/octopus-artifacts.md`,
  shipped like the existing `octopus-deployment-context` system skill. Provides
  gating + instructions to the agent.
- **`ArtifactManifestCollector`** — new class. Reads the manifest, validates each
  entry, copies valid files out of the temp dir, emits the service message.
  Invoked from `InvokeClaudeCodeBehaviour.Execute` after `RunAsync` returns and
  **before** the `using var tempDir` disposes.
- No new SpecialVariables, no MCP server, no new package dependencies.

### Flow

```
Step author prompt: "...create summary.csv and attach it as an artifact"
        │
        ▼
Claude (headless CLI, cwd = workingDir)
        │  recognises intent via the always-present "octopus-artifacts" skill
        ▼
Writes file(s)/dir under workingDir + appends a line per entry to
        │                              workingDir/.octopus/artifacts.jsonl
        ▼   (after RunAsync returns, before tempDir disposes)
ArtifactManifestCollector ── reads manifest, validates each entry (HARD FAIL on bad)
        │                     file → copy out to calamariDir/artifacts/<relpath>
        │                     dir  → zip out to calamariDir/artifacts/<dirname>.zip
        ▼
log.NewOctopusArtifact(outPath, name, length) ── one per valid entry
```

### Manifest format

Location: `workingDir/.octopus/artifacts.jsonl`. One JSON object per line:

```json
{"path": "summary.csv", "name": "Daily Summary"}
{"path": "output/data.csv"}
{"path": "site", "name": "Generated Website"}
```

- `path`: relative to `workingDir` (absolute paths that normalise to within
  `workingDir` are also accepted). Required, non-empty. May reference a **file**
  or a **directory**.
- `name`: optional display name for the artifact; defaults to the file name, or
  `<dirname>.zip` for a directory entry.
- JSONL chosen so the agent can append incrementally and each entry is validated
  independently.

**Multiple files vs. a tree:**

- *Several discrete files* (*"create files that…"*) → the agent appends one entry
  per file; each becomes its own artifact.
- *A whole tree* (*"create a website that…"*) → the agent puts the deliverable in
  a dedicated subdirectory and adds a single entry for that directory, which is
  zipped into one bundle artifact. The agent must **not** attach the working-dir
  root (see validation), so scaffolding never ends up in a bundle.

### Validation (hard-fail)

The step fails with a `CommandException` if any manifest entry is invalid:

- the line is not valid JSON, or `path` is empty;
- the referenced file or directory does not exist;
- the **canonical real path** (symlinks and `../` segments resolved) is not under
  the canonical `workingDir`;
- the path resolves to the **working-dir root itself** (must be a strict subpath —
  this prevents bundling scaffolding such as `deployment-variables.json`,
  `mcp-config.json`, the system prompt, or the manifest itself);
- the path is a directory that is empty.

If the manifest file is absent or empty, the collector is a no-op and the step
proceeds normally.

> **Watch item:** hard-fail means a single bad/hallucinated entry fails an
> otherwise-successful (and already paid-for) run. If this proves problematic in
> practice, retreat to a hybrid: skip missing/malformed entries with a warning,
> but still hard-fail on an out-of-bounds path (which signals injection or
> misconfiguration).

### Capture & emit

`calamariDir = context.CurrentDirectory` outlives the temp dir, matching the
debug-log durability pattern. Preserving the relative path avoids basename
collisions between two attached files. The agent's workspace is left intact (copy,
not move) for any subsequent logic.

- **File entry** → copy to `calamariDir/artifacts/<relativePathFromWorkingDir>`,
  then emit
  `log.NewOctopusArtifact(destPath, name ?? fileName, new FileInfo(destPath).Length)`.
- **Directory entry** → zip the directory (preserving internal structure) to
  `calamariDir/artifacts/<relativePathFromWorkingDir>.zip`, then emit
  `log.NewOctopusArtifact(zipPath, name ?? "<dirname>.zip", new FileInfo(zipPath).Length)`.
  One artifact per directory entry, regardless of how many files it contains.

### Security boundary

Working-dir-only for v1 (covers the "agent generates a deliverable" case, since
those files are created under `workingDir` anyway). May expand to also include the
Calamari deployment directory (`context.CurrentDirectory`) later if useful.
Arbitrary readable paths are explicitly out of scope to avoid a prompt-injection
exfiltration vector (e.g. attaching `/etc/passwd`).

### The skill (always on)

`octopus-artifacts.md`, drafted as:

> **name:** octopus-artifacts
> **description:** Use ONLY when the user explicitly asks to attach, upload, or
> save output as an Octopus artifact.
>
> Body: create the output inside the current working directory; then append a line
> to `.octopus/artifacts.jsonl` (create the file/dir if needed) for each artifact:
> `{"path": "<relative path>", "name": "<optional display name>"}`.
> - For several individual files, add **one entry per file**.
> - For many related files (e.g. a website), put them in a **dedicated
>   subdirectory** and add a single entry for that directory — it will be zipped
>   into one artifact. Do not attach the working directory root.
>
> Do this only on explicit request — never infer it.

Instructing the agent to create artifact output within the working directory (and
to use a subdirectory for trees) aligns its default behaviour with the validation
boundary, minimising "rejected" surprises.

## Testing

Unit tests on `ArtifactManifestCollector`, following the existing
extension-method / `IVariables` testing style:

- a single valid file entry is copied out and a `NewOctopusArtifact` service
  message is emitted;
- **multiple file entries** → each copied out, one service message per entry;
- **directory entry** → zipped to a single `.zip` artifact, one service message,
  internal structure preserved;
- missing file/directory → `CommandException`;
- path traversal / symlink escaping `workingDir` → `CommandException`;
- entry resolving to the working-dir **root** → `CommandException`;
- empty directory entry → `CommandException`;
- malformed line / empty path → `CommandException`;
- absent or empty manifest → no-op, no service messages;
- `name` omitted → display name defaults to the file name (file) / `<dirname>.zip`
  (directory);
- two files sharing a basename → relative-path preservation keeps them distinct.

## Follow-ups

- **ADR:** capture the "explicit + Calamari-managed; manifest over in-process MCP
  for v1" decision and its trade-offs in the
  [adr repo](https://github.com/OctopusDeploy/adr). Use the `adr-review` skill
  against the design conversation.
- Revisit the declaration surface (in-process MCP tool) if the manifest proves
  unreliable.
- Revisit hard-fail vs. hybrid error handling if a single bad entry failing a run
  becomes a problem.
- Consider expanding the path boundary to the Calamari deployment directory.
