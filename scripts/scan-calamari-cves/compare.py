#!/usr/bin/env python3
"""Aggregate per-version summaries, compare against previous state, report changes.

Called by scan.sh after every version has been scanned. Reads
<workdir>/<label>/summary.json plus <workdir>/previous.json, writes
<workdir>/summary.json (the new state) and <workdir>/slack.txt (the notification
body), and prints the human diff.

Exit code 3 means "the answer changed" - that is the actionable event for a
scheduler. Exit 0 means nothing moved, so a nightly run stays quiet.
"""
import json
import sys
import urllib.request
from pathlib import Path

RELEASES_INDEX = "https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json"


def parse_version(v):
    parts = []
    for chunk in str(v).split("."):
        try:
            parts.append(int(chunk))
        except ValueError:
            parts.append(0)
    return tuple(parts)


def latest_patches():
    """{channel: latest runtime} from Microsoft, or {} if unreachable.

    Best effort on purpose. A scan that cannot reach GitHub should still report its
    CVE findings rather than fail.
    """
    try:
        with urllib.request.urlopen(RELEASES_INDEX, timeout=20) as r:
            doc = json.loads(r.read())
    except Exception:
        return {}
    out = {}
    for entry in doc.get("releases-index") or []:
        channel = entry.get("channel-version")
        latest = entry.get("latest-runtime")
        if channel and latest:
            out[channel] = {"latest": latest, "eol": entry.get("eol-date"),
                            "support": entry.get("support-phase")}
    return out


def runtime_notes(runtimes, patches):
    """Flag bundled runtimes that trail the current patch, or are out of support.

    This matters independently of any CVE. A self-contained app carries its own
    runtime, and Microsoft stops publishing advisories for out-of-support versions -
    so scanners go quiet on exactly the artifacts that deserve the most suspicion.
    A clean runtime scan on an old artifact is not evidence it is safe.
    """
    notes = []
    for rt in runtimes:
        channel = ".".join(str(rt).split(".")[:2])
        info = patches.get(channel)
        if not info:
            continue
        if parse_version(rt) < parse_version(info["latest"]):
            notes.append(f"{rt} trails the current {channel} patch ({info['latest']})")
        phase = (info.get("support") or "").lower()
        if phase and phase not in ("active", "maintenance", "lts"):
            notes.append(f"{rt} is {phase}" + (f", EOL {info['eol']}" if info.get("eol") else ""))
    return notes


def main():
    workdir = Path(sys.argv[1])
    labels = sys.argv[2:]

    state = {}
    for label in labels:
        path = workdir / label / "summary.json"
        if path.exists():
            s = json.loads(path.read_text())
            state[label] = {"runtime": s.get("runtime", []), "cves": s.get("cves", [])}

    try:
        previous = json.loads((workdir / "previous.json").read_text() or "{}")
        if not isinstance(previous, dict):
            previous = {}
    except Exception:
        previous = {}

    patches = latest_patches()

    changed = False
    lines = []

    # Drift is folded into the state rather than reported standalone. A runtime that
    # permanently trails the current patch would otherwise alert on every single run,
    # which is how an alert becomes something everyone mutes.
    for label, now in state.items():
        now["drift"] = runtime_notes(now["runtime"], patches)

    for label in labels:
        now = state.get(label)
        if now is None:
            continue
        before = previous.get(label)
        cves_now = set(now["cves"])

        if before is None:
            # No baseline for this version. Report it once so the first run establishes
            # what "normal" looks like, rather than silently adopting it.
            changed = True
            lines.append(f"*{label}* - first scan, establishing the baseline")
            lines.append(f"  runtime: {', '.join(now['runtime']) or 'none found'}")
            lines.append(f"  {len(cves_now)} distinct CVE(s)"
                         + (": " + ", ".join(sorted(cves_now)) if cves_now else ""))
            for note in now["drift"]:
                lines.append(f"  runtime drift: {note}")
            continue

        cves_before = set(before.get("cves") or [])
        added = sorted(cves_now - cves_before)
        removed = sorted(cves_before - cves_now)
        rt_before = list(before.get("runtime") or [])
        rt_changed = rt_before != list(now["runtime"])
        drift_before = list(before.get("drift") or [])
        drift_new = [d for d in now["drift"] if d not in drift_before]

        if added or removed or rt_changed or drift_new:
            changed = True
            lines.append(f"*{label}*")
            if added:
                lines.append(f"  NEW: {', '.join(added)}")
            if removed:
                lines.append(f"  gone: {', '.join(removed)}")
            if rt_changed:
                lines.append(f"  runtime: {', '.join(rt_before) or 'none'}"
                             f" -> {', '.join(now['runtime']) or 'none'}")
            for note in drift_new:
                lines.append(f"  runtime drift: {note}")

    print("\n\033[1mCompared against previous state\033[0m" if sys.stdout.isatty()
          else "\nCompared against previous state")
    if not previous:
        print("  no previous state supplied - this run establishes the baseline")
    if changed:
        for line in lines:
            print("  " + line.replace("*", ""))
    else:
        print("  no change: same distinct CVE set, same bundled runtime")

    (workdir / "summary.json").write_text(json.dumps(state, indent=2, sort_keys=True))
    (workdir / "slack.txt").write_text("\n".join(lines) if lines else "No change.")

    sys.exit(3 if changed else 0)


if __name__ == "__main__":
    main()
