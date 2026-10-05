"""Runs the evaluation suite programmatically and writes results/latest.json.

Usage (from agentic-service/):
    python -m tests.evaluation.run_eval
"""
import json
import os
import sys
import tempfile
from datetime import datetime, timezone

import pytest

HERE = os.path.dirname(os.path.abspath(__file__))
RESULTS_DIR = os.path.join(HERE, "results")
RESULTS_FILE = os.path.join(RESULTS_DIR, "latest.json")
DEFENSES_FILE = os.path.join(tempfile.gettempdir(), "petcare_defenses.jsonl")


class _Collector:
    """pytest plugin capturing per-test outcomes."""

    def __init__(self):
        self.outcomes = {}

    def pytest_runtest_logreport(self, report):
        if report.when != "call":
            return
        nodeid = report.nodeid
        name = nodeid.split("::")[-1]
        # parametrized cases: strip the [payload] suffix but keep grouping
        base = name.split("[")[0]
        passed = report.outcome == "passed"
        self.outcomes[nodeid] = {"name": name, "base": base, "passed": passed}


def _load_defenses():
    defenses = {}
    if not os.path.exists(DEFENSES_FILE):
        return defenses
    with open(DEFENSES_FILE, encoding="utf-8") as fh:
        for line in fh:
            line = line.strip()
            if not line:
                continue
            record = json.loads(line)
            defenses[record["case"]] = record["defense"]
    return defenses


def main() -> int:
    os.makedirs(RESULTS_DIR, exist_ok=True)
    if os.path.exists(DEFENSES_FILE):
        os.remove(DEFENSES_FILE)

    collector = _Collector()
    exit_code = pytest.main(
        [HERE, "-q", "--tb=short", "-p", "no:cacheprovider"],
        plugins=[collector],
    )

    defenses = _load_defenses()
    cases = [
        {
            "name": item["name"],
            "passed": item["passed"],
            **({"defense": defenses[item["base"]]}
               if item["base"] in defenses else {}),
        }
        for item in sorted(collector.outcomes.values(), key=lambda i: i["name"])
    ]
    # Fallback: defenses recorded under a case name that matches the file-level
    # grouping (e.g. "prompt_injection" shared across parametrized runs).
    for case in cases:
        if "defense" in case:
            continue
        for key, value in defenses.items():
            if key in case["name"]:
                case["defense"] = value
                break

    passed = sum(1 for c in cases if c["passed"])
    payload = {
        "generatedAt": datetime.now(timezone.utc).isoformat(),
        "totals": {"total": len(cases), "passed": passed,
                   "failed": len(cases) - passed},
        "cases": cases,
    }
    with open(RESULTS_FILE, "w", encoding="utf-8") as fh:
        json.dump(payload, fh, indent=2)

    print(json.dumps(payload["totals"]))
    print(f"wrote {RESULTS_FILE}")
    return int(exit_code)


if __name__ == "__main__":
    sys.exit(main())
