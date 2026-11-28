# Y8: pipeline-uitbreiding (PR #15) en smoke-test-fix (PR #18)

Datum: 15 en 17 november 2026. Geen geheimen in dit bestand.

## Verloop

| Stap | Resultaat |
|---|---|
| PR #15 (`opdracht4-pipeline`): jobs `openfga-model`, `compose-validate`, smoke-test na deploy, `concurrency` | Eerste CI-run: `sast-semgrep` faalde met exit code 3 (Semgrep `--strict` kan de bash-constructie `case ... 80\|443)` niet parsen). Opgelost door een `grep`-controle. |
| PR #15 gemerged op 15 nov, deploy-run 43733047777 | **Rood.** De deploy zelf slaagde; de smoke-test faalde op `keycloak discovery (master) 403`. |
| Analyse | Verkeerde verwachting in de test, geen fout in de applicatie. De master-realm is bewust niet publiek (tunnel-only, Y3b); een publieke discovery van `master` geeft dus 403. De app-realm `unbreakable` is wel publiek (200, controle op de server). |
| PR #18 (`opdracht4-smoke-fix`) | Test aangepast: discovery van `unbreakable` verwacht 200 en de issuer moet het publieke adres zijn; discovery van `master` verwacht 403. CI-run PR-event 43733873684: groen. |
| PR #18 gemerged op 17 nov, deploy-run 44090353535 | **Groen**, inclusief smoke-test. |
| PR #17 en #16 (Jalil) gemerged na "main in de branch mergen" | Alle zes verplichte checks groen; deploy-runs 44092442474 en 44093084405 groen. |

## Smoke-test (wat de pipeline na elke deploy controleert)

- `/health` geeft 200.
- `/api/messages` zonder token geeft 401.
- Discovery van `unbreakable` geeft 200 en de issuer klopt.
- Discovery van `master` geeft 403.
- `/auth/admin/` publiek geeft 403.
- Enkel 80 en 443 zijn publiek gepubliceerd.

## Les

Een smoke-test moet het ontworpen gedrag toetsen. De rode run toonde dat de test het oude gedrag (publieke master-discovery) veronderstelde, niet dat de beveiliging stuk was. Daarnaast: Semgrep `--strict` behandelt een parserfout als falen, dus shell in workflows blijft eenvoudig.

Run-URL's: `https://github.com/yorbengeerinck/websec-unbreakable-g05/actions/runs/<id>` met de ID's hierboven.
