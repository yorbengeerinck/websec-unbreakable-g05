# Geaccepteerde bevindingen eerste run (g05)
Datum: 2026-10-06, ongewijzigde starter app

## SCA (dotnet list) - DSOMM: Patch Management | Dependency vulnerability scanning
Kritiek = High of Criticalseverity.
- Newtonsoft.Json 12.0.1 - High - GHSA-5crp-9r3c-p9vr - ECHT probleem (transitive via starter template), mag nu: alleen lab, geen prod data, fix = major bump naar 13.x in latere opdracht. Geaccepteerd.
- Nieuwe package met High/Critical buiten deze lijst = pipeline FAIL.

## SAST (Semgrep) - DSOMM: Secure Build | SAST
- SQLi in app/Program.cs search met FromSqlRaw - ECHT, mag nu (WAF ervoor, fix later).
- X-User header spoofing / missing auth - ECHT, mag nu.
- CORS AllowAnyOrigin - ECHT, mag nu voor demo SPA.

## Secrets (Gitleaks) - DSOMM: Secrets Management | Secret scanning
- Unbr3akable!2026 in docker-compose.yml - ECHT lab-geheim, mag nu, zie .gitleaks.toml allowlist.
