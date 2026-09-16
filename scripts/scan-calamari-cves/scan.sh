#!/usr/bin/env bash
# Reproduce what a customer's vulnerability scanner reports against Calamari.
#
# Customers scan the files on their deployment targets, not this repo and not the
# NuGet graph. That distinction matters: `dotnet list package --vulnerable` reports
# build-time reference shims that contribute no runtime assembly and that customer
# scanners never see. This script scans the real shipped artifact instead.
#
# Usage:
#   ./scan.sh                      # scan the latest main-branch CI build from feedz
#   ./scan.sh 2026.3.508           # scan a specific published version
#   ./scan.sh --local              # publish from the working tree and scan that
#
# Requires: docker (or OrbStack), curl, unzip, python3. dotnet only for --local.

set -euo pipefail

WORKDIR="${TMPDIR:-/tmp}/calamari-cve-scan"
FEED="https://f.feedz.io/octopus-deploy/dependencies/nuget/v3"
PKG="octopus.calamari.consolidated"
MODE="feed"
VERSION=""

for arg in "$@"; do
  case "$arg" in
    --local) MODE="local" ;;
    --help|-h) sed -n '2,15p' "$0" | sed 's/^# \{0,1\}//'; exit 0 ;;
    *) VERSION="$arg" ;;
  esac
done

say() { printf '\n\033[1m%s\033[0m\n' "$*"; }
note() { printf '  %s\n' "$*"; }

rm -rf "$WORKDIR"; mkdir -p "$WORKDIR/scan"

if [ "$MODE" = "local" ]; then
  say "Publishing from the working tree (self-contained linux-x64, as shipped)"
  REPO_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
  dotnet publish "$REPO_ROOT/source/Calamari/Calamari.csproj" \
    -c Release -f net8.0 -r linux-x64 --self-contained true \
    -o "$WORKDIR/scan" >/dev/null
  note "published $(find "$WORKDIR/scan" -name '*.dll' | wc -l | tr -d ' ') DLLs"
  note "NOTE: this uses YOUR SDK's runtime pack, which may differ from CI's."
  note "      Compare against a feed scan before drawing conclusions."
else
  if [ -z "$VERSION" ]; then
    say "Finding the latest published Calamari"
    VERSION=$(curl -fsS "$FEED/registration/$PKG/index.json" \
      | python3 -c "import json,sys,urllib.request
d=json.load(sys.stdin); last=d['items'][-1]
items=last.get('items')
if items is None:
    with urllib.request.urlopen(last['@id']) as r: items=json.loads(r.read()).get('items',[])
vs=[(i.get('catalogEntry') or {}).get('version','') for i in items]
vs=[v for v in vs if v and '-' not in v]
print(vs[-1] if vs else '')")
    [ -z "$VERSION" ] && { echo "could not determine latest version" >&2; exit 1; }
  fi
  note "version: $VERSION"

  say "Downloading the shipped package (this is what customers receive)"
  curl -fsS --max-time 600 -o "$WORKDIR/cal.nupkg" \
    "$FEED/packages/$PKG/$VERSION/$PKG.$VERSION.nupkg"
  note "$(du -h "$WORKDIR/cal.nupkg" | awk '{print $1}')"

  say "Extracting"
  mkdir -p "$WORKDIR/pkg"
  ( cd "$WORKDIR/pkg" && unzip -qq ../cal.nupkg )
  INNER=$(find "$WORKDIR/pkg/contentFiles" -name '*.zip' | head -1)
  [ -z "$INNER" ] && { echo "no inner payload found" >&2; exit 1; }
  unzip -qq "$INNER" -d "$WORKDIR/scan"
  note "$(find "$WORKDIR/scan" -type f | wc -l | tr -d ' ') files"
fi

say "Bundled .NET runtime (self-contained, so this ships to every target)"
# -I skips binaries; the deps.json files carry the authoritative version and the
# self-contained helper binaries would otherwise emit "Binary file ... matches" noise.
grep -rhoIE 'runtimepack\.Microsoft\.NETCore\.App\.Runtime\.[a-z0-9-]+/[0-9.]+' "$WORKDIR/scan" 2>/dev/null \
  | sort -u | sed 's/^/  /' || note "none found"

say "Trivy"
docker run --rm -v "$WORKDIR/scan":/scan:ro -v "$WORKDIR/trivy-cache":/root/.cache \
  --platform linux/amd64 aquasec/trivy:latest fs --scanners vuln --format json --quiet /scan \
  > "$WORKDIR/trivy.json" 2>/dev/null || true
python3 - "$WORKDIR/trivy.json" <<'PY'
import json,sys
try: d=json.load(open(sys.argv[1]))
except Exception: print("  (no output)"); raise SystemExit
seen=set(); total=0
for r in d.get('Results') or []:
    for v in (r.get('Vulnerabilities') or []):
        total+=1
        seen.add((v.get('Severity'),v.get('VulnerabilityID'),v.get('PkgName'),v.get('InstalledVersion'),v.get('FixedVersion')))
print(f"  {total} matches across all flavours -> {len(seen)} DISTINCT")
for s,c,p,i,f in sorted(seen):
    print(f"  {s:9s} {c:22s} {p:38s} {i:14s} fixed: {f}")
PY

say "Grype (second opinion, different vulnerability database)"
docker run --rm -v "$WORKDIR/scan":/scan:ro -v "$WORKDIR/grype-cache":/root/.cache \
  --platform linux/amd64 anchore/grype:latest dir:/scan -o json -q \
  > "$WORKDIR/grype.json" 2>/dev/null || true
python3 - "$WORKDIR/grype.json" <<'PY'
import json,sys
try: d=json.load(open(sys.argv[1]))
except Exception: print("  (no output)"); raise SystemExit
seen=set()
for m in d.get('matches') or []:
    v=m.get('vulnerability') or {}; a=m.get('artifact') or {}
    seen.add((str(v.get('severity')),str(v.get('id')),str(a.get('name')),str(a.get('version'))))
print(f"  {len(d.get('matches') or [])} matches -> {len(seen)} DISTINCT")
for s,c,p,ver in sorted(seen):
    print(f"  {s:9s} {c:22s} {p:38s} {ver}")
PY

say "Done"
note "artifacts kept in $WORKDIR (trivy.json / grype.json for raw detail)"
note "If the two scanners disagree, prefer investigating over dismissing —"
note "they use different databases and different matching rules."
