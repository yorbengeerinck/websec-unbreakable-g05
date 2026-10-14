## Stap 5 — BunkerWeb WAF

### Doel

- Applicatie beveiligen met een Web Application Firewall (WAF).
- HTTPS inschakelen met een Let's Encrypt-certificaat.
- Applicatie alleen bereikbaar maken via BunkerWeb.
- Poort 8080 niet meer rechtstreeks beschikbaar stellen.

### Configuratie

**Docker Compose (`docker-compose.yml`)**

- `bunkerweb`: ontvangt HTTP- en HTTPS-verkeer.
- `bw-scheduler`: beheert de BunkerWeb-configuratie en automatische certificaten.
- `app`: draait de .NET-applicatie op poort 8080.
- `db`: PostgreSQL-database.

**Aanpassingen**

- Bij `app` werd `ports` vervangen door `expose: "8080"`.
  - Hierdoor wordt poort 8080 niet meer op de host gepubliceerd.
- BunkerWeb werd toegevoegd als reverse proxy.
  - Hostpoort 80 → BunkerWeb.
  - Hostpoort 443 → BunkerWeb.
  - BunkerWeb stuurt verzoeken door naar `http://app:8080`.
- Docker-netwerken toegevoegd om de communicatie tussen containers te scheiden.
- Let's Encrypt ingeschakeld met `AUTO_LETS_ENCRYPT: "yes"`.

### Domeinnaam en omgevingsvariabelen

- Domeinnaam: `g05.ipv6.thelastgym.be`.
- De domeinnaam verwijst naar het IPv6-adres van onze server.
- `APP_DOMAIN` wordt ingesteld via een `.env`-bestand op de server.
- Bestand staat buiten de Git-repository:

  `/etc/unbreakable/.env`

Hierdoor blijft de serverconfiguratie behouden bij een nieuwe deployment.

### GitHub Actions

De deployment werd aangepast zodat Docker Compose het `.env`-bestand van de server gebruikt:

```bash
docker compose --env-file /etc/unbreakable/.env up -d --build
```

Na een succesvolle securitypipeline wordt de applicatie automatisch opnieuw gedeployed.

### Uitgevoerde tests

| Test | Resultaat |
|---|---|
| Automatische deployment | Geslaagd |
| HTTPS-verbinding | Geldig certificaat |
| Normaal verzoek naar `/health` | HTTP 200 |
| XSS-teststring in URL | HTTP 403 |
| Rechtstreekse publicatie van poort 8080 | Niet gepubliceerd |

**XSS-test**

```text
/health?test=%3Cscript%3Ealert(1)%3C%2Fscript%3E
```

- Normaal verzoek werd toegelaten.
- Verdacht verzoek kreeg HTTP 403.
- Dit wijst erop dat BunkerWeb het verdachte verzoek blokkeert.

### Nog te controleren

- In de WAF-logs bevestigen welke regel de HTTP 403 veroorzaakte.
- Een test uitvoeren via het zoekformulier van de applicatie.
- Bereikbaarheid vanuit de browser op onze eigen pc regelen via SSH-tunneling.
