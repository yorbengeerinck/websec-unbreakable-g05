# Geaccepteerde bevindingen eerste run (g05)
Datum: 2026-10-06, ongewijzigde starter app

## SCA (dotnet list) - DSOMM: Patch Management | Dependency vulnerability scanning
Kritiek = High of Critical severity.
- Newtonsoft.Json 12.0.1 - High - GHSA-5crp-9r3c-p9vr - ECHT, mag nu: alleen lab, fix naar 13.x later.

## SAST (Semgrep) - DSOMM: Secure Build | SAST
- SQLi in app/Program.cs search met FromSqlRaw - ECHT, mag nu (WAF ervoor, fix later).
- X-User header spoofing / missing auth - ECHT, mag nu.
- CORS AllowAnyOrigin - ECHT, mag nu.

## Secrets (Gitleaks) - DSOMM: Secrets Management | Secret scanning
- Unbr3akable!2026 in docker-compose.yml + app/appsettings.json connectionstring - ECHT lab-geheim, mag nu, allowlisted.
- ubk_live_7Hq2Lx9Vt4Rm8Pz3Wc6Ny1Bf5Kd0Js in app/appsettings.json:15 generic-api-key - FAKE lab-key, geen prod, mag nu, allowlisted.
- Nieuwe secret buiten deze lijst = pipeline FAIL.
