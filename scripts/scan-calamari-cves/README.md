# Reproducing what customer scanners report against Calamari

Run `./scan.sh`. Takes about ten minutes, most of it downloading a ~270 MB package.

```bash
./scan.sh                # latest main-branch CI build
./scan.sh 2026.3.508     # a specific published version
./scan.sh --local        # publish from your working tree and scan that
```

## Why this rather than `dotnet list package --vulnerable`

**They answer different questions, and the local one over-reports.**

Customers scan the files on their deployment targets. Calamari publishes
**self-contained**, so a target holds ~345 DLLs including a full private copy of the .NET
runtime. That is the scan surface.

`dotnet list package --vulnerable` walks the NuGet *graph*, which includes build-time
reference shims — `System.Net.Http 4.3.0`, `System.Text.RegularExpressions 4.3.0` — pulled
in transitively via `NETStandard.Library`. Those contribute **no runtime assembly**
(`runtime: []` in the shipped `.deps.json`, and the DLLs are absent from build output), so
no customer scanner ever sees them. Dependabot doesn't report them either.

If someone asks about a CVE that only appears in the local command, that's the explanation.

## What the script does

1. Downloads the real published `Octopus.Calamari.Consolidated` package from feedz.
2. Extracts it, including the inner consolidated archive containing every flavour and RID.
3. Prints the **bundled .NET runtime version** — usually the single most important number,
   since a self-contained app ships its own runtime and inherits its CVEs.
4. Scans with **Trivy** and **Grype** — two tools, two vulnerability databases. Customer
   scanners disagree with each other, so one tool is not a baseline.
5. Reports total matches and *distinct* CVEs. These differ a lot: the same finding repeats
   across ~43 `deps.json` files, which is why a report of "42 vulnerabilities" can be one
   issue.

## Reading the results

**Distinct count is the real number.** Total matches counts each flavour separately.

**Check the runtime version first.** It's the largest single contributor. A self-contained
app carries its own runtime, so an artifact built months ago carries a months-old runtime
with every CVE published since.

**Findings are version-dependent, and that's usually the answer.** Measured 2026-08-01:

| Calamari | Distinct CVEs | Bundled .NET |
|---|---|---|
| `2025.3.417` | **9** (1 critical, 5 high) | 6.0.36 — **EOL Nov 2024** |
| `2026.3.508` | **1** (medium) | 8.0.29 — current patch |

If a customer reports many CVEs, check their Calamari version before anything else.

**Beware the EOL trap.** The 2025.3.417 scan showed *zero* runtime CVEs despite bundling an
end-of-life .NET 6. Microsoft stops publishing advisories for out-of-support versions, so
scanners go quiet. **A clean runtime scan on an old artifact is not evidence it is safe** —
it usually means nobody is looking any more.

**Reported ≠ exploitable.** A finding against a shipped DLL says the version matches an
advisory, not that the vulnerable path is reachable. Assessing that means reading how
Calamari calls the library. Worked example: `CVE-2026-44788` (SharpCompress) is reported,
but the vulnerable path is the archive-level `IArchive.WriteToDirectory()`, while Calamari
iterates entries itself and calls the per-entry APIs the advisory names as guarded — and
`ThrowIfPathTraversalAttempted` bounds-checks every entry key before any write. The
regression test for it passes on the *vulnerable* version, which is the proof.

## Caveats

- Both tools key off `deps.json`. A scanner that fingerprints raw DLL file versions may
  report differently.
- `--local` uses *your* SDK's runtime pack, which may differ from CI's. A local publish
  showed 8.0.27 with seven HIGH runtime CVEs while CI shipped 8.0.29 with none. Always
  compare against a feed scan before reporting anything.
- Vulnerability databases move daily. `CVE-2026-44788` was absent from the NuGet audit
  source at 10:45 and present by 15:00 on the same day. Re-run rather than cite an old
  result.
