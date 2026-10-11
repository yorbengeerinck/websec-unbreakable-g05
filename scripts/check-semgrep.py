#!/usr/bin/env python3
"""Fail CI on new high-severity Semgrep findings not in the reviewed baseline."""
import json
import sys
from pathlib import Path

RESULTS = Path('semgrep-results.json')
BASELINE = Path('.semgrep-baseline.json')
BLOCKING_SEVERITIES = {'ERROR', 'CRITICAL', 'HIGH'}


def identity(finding):
    """Stable enough for the current baseline; moving code requires re-review."""
    return (
        finding['check_id'],
        finding['path'],
        finding['start']['line'],
        finding['end']['line'],
    )


def main():
    try:
        results = json.loads(RESULTS.read_text(encoding='utf-8'))
        baseline = json.loads(BASELINE.read_text(encoding='utf-8'))
        if results.get('errors'):
            print('Semgrep reported scan errors; refusing deployment.')
            return 1
        reviewed = set()
        for entry in baseline['accepted_findings']:
            if not entry.get('reason', '').strip():
                raise ValueError('Every accepted finding needs a documented reason')
            reviewed.add((entry['check_id'], entry['path'], entry['start_line'], entry['end_line']))
        findings = results['results']
        new = [f for f in findings if identity(f) not in reviewed]
        blocking = [f for f in new if f['extra']['severity'].upper() in BLOCKING_SEVERITIES]
        print(f'Semgrep: {len(findings)} findings; {len(new)} not in reviewed baseline; {len(blocking)} new blocking findings.')
        for f in new:
            severity = f['extra']['severity']
            print(f"NEW {severity}: {f['check_id']} at {f['path']}:{f['start']['line']}")
        if blocking:
            print('FAIL: New high-severity findings require review.')
            return 1
        print('PASS: No new high-severity findings.')
        return 0
    except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError) as exc:
        print(f'Cannot verify Semgrep baseline: {exc}', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
