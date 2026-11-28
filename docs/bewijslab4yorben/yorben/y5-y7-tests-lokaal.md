# Y5-Y7: API-tests lokaal

Datum: 2026-11-11. Machine: Windows, .NET SDK 10.0.404, PostgreSQL 16 in Docker (`pgtest`).

Commando:

    $env:TEST_CONNECTIONSTRING = "Host=localhost;Port=5432;Database=unbreakable_test;Username=postgres;Password=<lokaal testwachtwoord>"
    dotnet test .\tests\Unbreakable.Tests\Unbreakable.Tests.csproj

Eerste run (commit 040990a): 11 geslaagd, 2 mislukt.
- T21 (PDP onbereikbaar): kreeg 500 in plaats van 503, door de volgorde van de middleware (developer exception page ving de fout eerst op).
- T17/T19/T20 (export): test las `id` terwijl de export `Id` (PascalCase, zoals de legacy-export) teruggeeft.

Herstel in commit cfcbadb. Tweede run:

    Test summary: total: 13; failed: 0; succeeded: 13; skipped: 0; duration: 3,7s
    Build succeeded in 6,2s

Het logboek van de tweede run toont de auditregels, bijvoorbeeld:

    authz action=read sub=sub-jalil message=1 decision=deny
    authz action=delete sub=sub-pros message=1 decision=deny
    authz action=create sub=sub-yorben message=2 decision=error   (PDP onbereikbaar, aanvraag geweigerd)

Gedekte gevallen: geen token, ongeldige tokens (T14-T16), alleen ontvanger leest (T04-T08), alleen afzender verwijdert (T10-T12), inbox/export bevatten enkel ontvangen berichten (T17-T20), PDP uit geeft 503 zonder data (T21), X-User-header heeft geen effect, mass assignment en validatie.
Nog niet gedekt: tests tegen een echte OpenFGA (T22, T23) en `fga tuple read`, die volgen na Y4 op de server.
