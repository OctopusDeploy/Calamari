#!/usr/bin/env bash
# Reproduce what a customer's vulnerability scanner reports against Calamari.
#
# Customers scan the files on their deployment targets, not this repo and not the
# NuGet graph. That distinction matters: `dotnet list package --vulnerable` reports
# build-time reference shims that contribute no runtime assembly and that customer
# scanners never see. This script scans the real shipped artifact instead.
#
# Usage:
#   ./scan.sh                          # scan the latest main-branch CI build from feedz
#   ./scan.sh 2026.3.508               # scan a specific published version
#   ./scan.sh 2025.3.417 2026.3.508    # scan several, e.g. the supported release tips
#   ./scan.sh --local                  # publish from the working tree and scan that
#
# Comparing against a previous result:
#   ./scan.sh --previous-state=old.json 2026.3.508
#
# Exits 3 when the reported CVE set differs from that previous state, so CI or a
# scheduler can treat "the answer changed" as the actionable event. A plain run with
# no previous state always exits 0.
#
# Requires: curl, unzip, python3, and either docker (default) or trivy+grype on PATH.
# dotnet only for --local.

set -euo pipefail

WORKDIR="${CALAMARI_SCAN_WORKDIR:-${TMPDIR:-/tmp}/calamari-cve-scan}"
FEED="https://f.feedz.io/octopus-deploy/dependencies/nuget/v3"
PKG="octopus.calamari.consolidated"
MODE="feed"
RUNNER="${SCAN_RUNNER:-auto}"
OCTOPUS=0
PREV_STATE_FILE=""
VERSIONS=()

for arg in "$@"; do
  case "$arg" in
    --local) MODE="local" ;;
    --octopus) OCTOPUS=1 ;;
    --runner=*) RUNNER="${arg#*=}" ;;
    --previous-state=*) PREV_STATE_FILE="${arg#*=}" ;;
    --help|-h) sed -n '2,24p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    --*) echo "unknown option: $arg" >&2; exit 2 ;;
    *) VERSIONS+=("$arg") ;;
  esac
done

# Only colour a real terminal. Octopus captures stdout into a task log, where raw
# escape codes show up verbatim and make the log harder to read, not easier.
if [ -t 1 ]; then BOLD=$'\033[1m'; RESET=$'\033[0m'; else BOLD=''; RESET=''; fi
say()  { printf '\n%s%s%s\n' "$BOLD" "$*" "$RESET"; }
note() { printf '  %s\n' "$*"; }

rm -rf "$WORKDIR"; mkdir -p "$WORKDIR"

# ---------------------------------------------------------------------------
# Scanners
#
# Two databases are used deliberately. Customer scanners disagree with each other,
# so one tool is not a baseline.
#
# Locally, running them as containers means no install step. Inside an Octopus
# execution container the step is *already* in a container, so docker-in-docker is
# not available and the binaries have to be on PATH instead.
# ---------------------------------------------------------------------------

if [ "$RUNNER" = "auto" ]; then
  if command -v trivy >/dev/null 2>&1 && command -v grype >/dev/null 2>&1; then
    RUNNER="native"
  elif command -v docker >/dev/null 2>&1; then
    RUNNER="docker"
  else
    RUNNER="native"  # nothing present; install below
  fi
fi

ensure_scanners() {
  if [ "$RUNNER" != "native" ]; then return 0; fi
  if command -v trivy >/dev/null 2>&1 && command -v grype >/dev/null 2>&1; then return 0; fi

  # NOTE: unpinned installs from upstream. Fine for an interactive run, weaker for a
  # scheduled one, where a scanner upgrade and a genuine new CVE look identical in the
  # diff. Baking both binaries into a pinned execution container image is the fix;
  # until then set TRIVY_VERSION / GRYPE_VERSION to pin.
  local bin="$WORKDIR/bin"; mkdir -p "$bin"; export PATH="$bin:$PATH"
  say "Installing scanners (no docker available)"
  command -v trivy >/dev/null 2>&1 || \
    curl -sSfL https://raw.githubusercontent.com/aquasecurity/trivy/main/contrib/install.sh \
      | sh -s -- -b "$bin" ${TRIVY_VERSION:+"$TRIVY_VERSION"} >/dev/null 2>&1
  command -v grype >/dev/null 2>&1 || \
    curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh \
      | sh -s -- -b "$bin" ${GRYPE_VERSION:+"$GRYPE_VERSION"} >/dev/null 2>&1
  note "trivy $(trivy --version 2>/dev/null | head -1)"
  note "grype $(grype version 2>/dev/null | grep -i '^version' || echo installed)"
}

run_trivy() { # <scan dir> <out file>
  if [ "$RUNNER" = "native" ]; then
    trivy fs --scanners vuln --format json --quiet "$1" > "$2" 2>/dev/null || true
  else
    docker run --rm -v "$1":/scan:ro -v "$WORKDIR/trivy-cache":/root/.cache \
      --platform linux/amd64 aquasec/trivy:latest fs --scanners vuln --format json --quiet /scan \
      > "$2" 2>/dev/null || true
  fi
}

run_grype() { # <scan dir> <out file>
  if [ "$RUNNER" = "native" ]; then
    grype "dir:$1" -o json -q > "$2" 2>/dev/null || true
  else
    docker run --rm -v "$1":/scan:ro -v "$WORKDIR/grype-cache":/root/.cache \
      --platform linux/amd64 anchore/grype:latest dir:/scan -o json -q \
      > "$2" 2>/dev/null || true
  fi
}

latest_published_version() {
  curl -fsS "$FEED/registration/$PKG/index.json" \
    | python3 -c "import json,sys,urllib.request
d=json.load(sys.stdin); last=d['items'][-1]
items=last.get('items')
if items is None:
    with urllib.request.urlopen(last['@id']) as r: items=json.loads(r.read()).get('items',[])
vs=[(i.get('catalogEntry') or {}).get('version','') for i in items]
vs=[v for v in vs if v and '-' not in v]
print(vs[-1] if vs else '')"
}

# ---------------------------------------------------------------------------
# Scan one version into $WORKDIR/<label>/
# ---------------------------------------------------------------------------
scan_one() { # <label> <version-or-empty>
  local label="$1" version="${2:-}" dir="$WORKDIR/$1"
  mkdir -p "$dir/scan"

  if [ "$MODE" = "local" ]; then
    say "Publishing from the working tree (self-contained linux-x64, as shipped)"
    local root; root="$(cd "$(dirname "$0")/../.." && pwd)"
    dotnet publish "$root/source/Calamari/Calamari.csproj" \
      -c Release -f net8.0 -r linux-x64 --self-contained true -o "$dir/scan" >/dev/null
    note "published $(find "$dir/scan" -name '*.dll' | wc -l | tr -d ' ') DLLs"
    note "NOTE: this uses YOUR SDK's runtime pack, which may differ from CI's."
    note "      Compare against a feed scan before drawing conclusions."
  else
    say "Downloading the shipped package for $version (this is what customers receive)"
    curl -fsS --max-time 600 -o "$dir/cal.nupkg" \
      "$FEED/packages/$PKG/$version/$PKG.$version.nupkg"
    note "$(du -h "$dir/cal.nupkg" | awk '{print $1}')"

    mkdir -p "$dir/pkg"
    ( cd "$dir/pkg" && unzip -qq ../cal.nupkg )
    local inner; inner=$(find "$dir/pkg/contentFiles" -name '*.zip' | head -1)
    if [ -z "$inner" ]; then echo "no inner payload found for $version" >&2; return 1; fi
    unzip -qq "$inner" -d "$dir/scan"
    note "$(find "$dir/scan" -type f | wc -l | tr -d ' ') files"
  fi

  say "Bundled .NET runtime for $label (self-contained, so this ships to every target)"
  # -I skips binaries; the deps.json files carry the authoritative version and the
  # self-contained helper binaries would otherwise emit "Binary file ... matches" noise.
  grep -rhoIE 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.[a-z0-9-]+/[0-9.]+' "$dir/scan" 2>/dev/null \
    | sort -u > "$dir/runtimes.txt" || true
  if [ -s "$dir/runtimes.txt" ]; then sed 's/^/  /' "$dir/runtimes.txt"; else note "none found"; fi

  note "scanning with trivy..."
  run_trivy "$dir/scan" "$dir/trivy.json"
  note "scanning with grype (second opinion, different vulnerability database)..."
  run_grype "$dir/scan" "$dir/grype.json"

  python3 "$(dirname "$0")/summarise.py" "$label" "$dir"
}

# ---------------------------------------------------------------------------

ensure_scanners
note "scanner runner: $RUNNER"

if [ "$MODE" = "local" ]; then
  scan_one "local" ""
  LABELS=("local")
else
  if [ ${#VERSIONS[@]} -eq 0 ]; then
    say "Finding the latest published Calamari"
    v="$(latest_published_version)"
    if [ -z "$v" ]; then echo "could not determine latest version" >&2; exit 1; fi
    VERSIONS=("$v")
  fi
  note "versions: ${VERSIONS[*]}"
  LABELS=()
  for v in "${VERSIONS[@]}"; do scan_one "$v" "$v"; LABELS+=("$v"); done
fi

# ---------------------------------------------------------------------------
# Aggregate, compare against previous state, report
# ---------------------------------------------------------------------------
PREV_JSON="{}"
if [ -n "$PREV_STATE_FILE" ] && [ -f "$PREV_STATE_FILE" ]; then
  PREV_JSON="$(cat "$PREV_STATE_FILE")"
elif [ "$OCTOPUS" = 1 ]; then
  PREV_JSON="$(get_octopusvariable "Calamari.CveScan.State" 2>/dev/null || echo '{}')"
  [ -z "$PREV_JSON" ] && PREV_JSON="{}"
fi
printf '%s' "$PREV_JSON" > "$WORKDIR/previous.json"

CHANGED=0
python3 "$(dirname "$0")/compare.py" "$WORKDIR" "${LABELS[@]}" || CHANGED=$?
if [ "$CHANGED" -ne 0 ] && [ "$CHANGED" -ne 3 ]; then exit "$CHANGED"; fi

if [ "$OCTOPUS" = 1 ]; then
  set_octopusvariable "Calamari.CveScan.State" "$(cat "$WORKDIR/summary.json")"
  set_octopusvariable "HasNewFindings" "$([ "$CHANGED" -eq 3 ] && echo true || echo false)"
  set_octopusvariable "SlackSummary" "$(cat "$WORKDIR/slack.txt")"
  new_octopusartifact "$WORKDIR/summary.json" "summary.json"
  for l in "${LABELS[@]}"; do
    if [ -s "$WORKDIR/$l/trivy.json" ]; then new_octopusartifact "$WORKDIR/$l/trivy.json" "trivy-$l.json"; fi
    if [ -s "$WORKDIR/$l/grype.json" ]; then new_octopusartifact "$WORKDIR/$l/grype.json" "grype-$l.json"; fi
  done
fi

say "Done"
note "artifacts kept in $WORKDIR (trivy.json / grype.json for raw detail)"
note "If the two scanners disagree, prefer investigating over dismissing -"
note "they use different databases and different matching rules."

exit "$CHANGED"
