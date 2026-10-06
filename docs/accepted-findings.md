# Geaccepteerde bevindingen eerste run (g05)
Datum: 2026-10-06, ongewijzigde starter app

## SCA (Trivy) DSOMM Patch Management
- TBD na eerste run: vul CVE + versie in. Reden voorbeeld: transitive dep van .NET base image, geen fix beschikbaar / alleen lab.

## SAST (Semgrep) DSOMM SAST
- SQLi in app/Program.cs search met FromSqlRaw -> ECHT probleem, mag nu (wordt afgeschermd door WAF, fix in latere opdracht).
- X-User header spoofing / missing auth -> ECHT, mag nu.
- CORS AllowAnyOrigin -> ECHT, mag nu voor demo SPA.

## Secrets (Gitleaks) DSOMM Secrets Management
- Unbr3akable!2026 in docker-compose.yml -> ECHT lab-geheim, mag nu, zie .gitleaks.toml allowlist.
