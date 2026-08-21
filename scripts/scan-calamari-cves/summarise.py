#!/usr/bin/env python3
"""Turn one version's raw Trivy and Grype output into a summary.

Called by scan.sh once per scanned version. Prints the human-readable table and
writes <dir>/summary.json for compare.py to aggregate.

Total matches and *distinct* CVEs differ a lot: the same finding repeats across
~43 deps.json files, one per flavour and RID. A customer reporting 42
vulnerabilities is routinely reporting one.
"""
import json
import re
import sys
from pathlib import Path


def load(path):
    try:
        return json.loads(Path(path).read_text())
    except Exception:
        return None


def trivy_findings(doc):
    """-> (total matches, {(severity, id, pkg, installed, fixed)})"""
    if not doc:
        return 0, set()
    total, seen = 0, set()
    for result in doc.get("Results") or []:
        for v in result.get("Vulnerabilities") or []:
            total += 1
            seen.add((
                v.get("Severity"), v.get("VulnerabilityID"), v.get("PkgName"),
                v.get("InstalledVersion"), v.get("FixedVersion"),
            ))
    return total, seen


def grype_findings(doc):
    if not doc:
        return 0, set()
    matches = doc.get("matches") or []
    seen = set()
    for m in matches:
        v = m.get("vulnerability") or {}
        a = m.get("artifact") or {}
        seen.add((str(v.get("severity")), str(v.get("id")),
                  str(a.get("name")), str(a.get("version"))))
    return len(matches), seen


def main():
    label, directory = sys.argv[1], Path(sys.argv[2])

    runtimes = []
    runtimes_file = directory / "runtimes.txt"
    if runtimes_file.exists():
        for line in runtimes_file.read_text().splitlines():
            m = re.search(r"/([0-9][0-9.]*)$", line.strip())
            if m:
                runtimes.append(m.group(1))
    runtimes = sorted(set(runtimes))

    t_total, t_seen = trivy_findings(load(directory / "trivy.json"))
    g_total, g_seen = grype_findings(load(directory / "grype.json"))

    print(f"\nTrivy ({label})")
    if not t_seen and t_total == 0:
        print("  (no output)")
    else:
        print(f"  {t_total} matches across all flavours -> {len(t_seen)} DISTINCT")
        for sev, cve, pkg, installed, fixed in sorted(t_seen, key=lambda r: tuple(str(x) for x in r)):
            print(f"  {str(sev):9s} {str(cve):22s} {str(pkg):38s} {str(installed):14s} fixed: {fixed}")

    print(f"\nGrype ({label})")
    if not g_seen and g_total == 0:
        print("  (no output)")
    else:
        print(f"  {g_total} matches -> {len(g_seen)} DISTINCT")
        for sev, cve, pkg, ver in sorted(g_seen, key=lambda r: tuple(str(x) for x in r)):
            print(f"  {sev:9s} {cve:22s} {pkg:38s} {ver}")

    # The union is what gets compared run to run. Either scanner flagging something is
    # enough to be worth a look - they use different databases and different matching
    # rules, so treating one as authoritative would quietly drop real findings.
    cves = sorted({c for _, c, *_ in t_seen} | {c for _, c, *_ in g_seen})

    summary = {
        "label": label,
        "runtime": runtimes,
        "cves": cves,
        "trivy": {"matches": t_total, "distinct": len(t_seen)},
        "grype": {"matches": g_total, "distinct": len(g_seen)},
    }
    (directory / "summary.json").write_text(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
