# Y8: branch protection op `main`

Datum: 2026-11-11. Instelling: GitHub, Settings, Branches, regel voor `main` (classic branch protection).

Screenshots: `y8-branch-protection-1.png` en `y8-branch-protection-2.png` (zelfde map).

## Ingestelde regels

| Regel | Waarde |
|---|---|
| Require a pull request before merging | aan |
| Require approvals | aan, 1 goedkeuring |
| Require status checks to pass before merging | aan |
| Verplichte checks | `backend-tests`, `sast-semgrep`, `sca-dotnet`, `secret-gitleaks` (GitHub Actions) |
| Do not allow bypassing the above settings | aan (geldt ook voor beheerders) |
| Allow force pushes | uit |
| Allow deletions | uit |

## Test: directe push naar `main`

Een lege commit werd lokaal op `main` gemaakt en rechtstreeks gepusht door de repo-eigenaar:

    git checkout main
    git pull --ff-only
    git commit --allow-empty -m "test: directe push naar main moet geweigerd worden"
    git push origin main

Uitvoer:

    remote: error: GH006: Protected branch update failed for refs/heads/main.
    remote:
    remote: - Changes must be made through a pull request.
    remote:
    remote: - 4 of 4 required status checks are expected.
    To https://github.com/yorbengeerinck/websec-unbreakable-g05.git
     ! [remote rejected] main -> main (protected branch hook declined)
    error: failed to push some refs to 'https://github.com/yorbengeerinck/websec-unbreakable-g05.git'

Conclusie: `main` is enkel bereikbaar via een PR met goedkeuring en groene checks. Ook de eigenaar kan dat niet omzeilen. De testcommit (`8c9bd2a`) bleef lokaal en is niet op GitHub terechtgekomen.
