# Y8: CI van PR #14 (opdracht4-api-authz)

PR: https://github.com/yorbengeerinck/websec-unbreakable-g05/pull/14, gemerged op 2026-11-11 10:55 UTC (merge commit e2ae52d).

## Run op de PR (commit cfcbadb)

Workflow `security-pipeline`, run 43007545176 (https://github.com/yorbengeerinck/websec-unbreakable-g05/actions/runs/43007545176)

| Job | Resultaat |
|---|---|
| sca-dotnet (strikt, faalt bij elk kwetsbaar package) | success |
| sast-semgrep | success |
| secret-gitleaks | success |
| backend-tests (Postgres 16 als service) | success |
| deploy | skipped (enkel op `main`) |

## Run op main na de merge (commit e2ae52d)

Run 43007655475 (https://github.com/yorbengeerinck/websec-unbreakable-g05/actions/runs/43007655475), conclusie: success.

| Job | Resultaat |
|---|---|
| sca-dotnet | success |
| sast-semgrep | success |
| secret-gitleaks | success |
| backend-tests | success |
| deploy (stap "Deploy app" en "Health check via WAF (HTTPS, met retries)") | success |

Het gate-gedrag staat in `.github/workflows/security.yml`: `deploy` heeft `needs: [sca-dotnet, sast-semgrep, secret-gitleaks, backend-tests]`.
