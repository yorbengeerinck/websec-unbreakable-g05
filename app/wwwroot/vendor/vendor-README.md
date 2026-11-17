# vendor

Externe JavaScript die we zelf meeleveren, zodat de SPA niets van een CDN laadt (supply chain en CSP `script-src 'self'`).

## keycloak-js

| | |
|---|---|
| Pakket | `keycloak-js` |
| Versie | 26.2.5 |
| Bron | npm registry, opgehaald met `npm pack keycloak-js@26.2.5` |
| Licentie | Apache-2.0 (zie `keycloak-js/LICENSE.txt`) |
| Doel | OIDC authorization code flow met PKCE in de SPA (`app/wwwroot/js/auth.js`) |
| Tarball shasum (sha1, van npm) | `a4c2f47c655c706ab32b473d8caa820387e1eff0` |

De versie van `keycloak-js` heeft een eigen nummer, los van de Keycloak-server (die draait op 26.8.2). Beide zijn 26.x. Of de combinatie werkt, bewijzen we bij de eerste login (J5, J9).

### Bestanden (sha256)

| Bestand | sha256 |
|---|---|
| `keycloak-js/keycloak.js` | `e77ac54c8501a4e93f054e3eb8c6bb0003b127004359c17bd4a22424c8d3e24e` |
| `keycloak-js/deprecations.js` | `afaac168f913d2a38bedac7517861ae61d44f60e189bd797dc61802d6dadab2d` |
| `keycloak-js/keycloak-dpop.js` | `45d40a8b5b6cf87d80b269d1c7f41aa14b8c838c8bce15345c1ca3219cdb6f86` |
| `keycloak-js/LICENSE.txt` | `cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30` |

### Opmerkingen

- Het pakket is een ES module. `auth.js` importeert het met `import Keycloak from '../vendor/keycloak-js/keycloak.js'`. Er is geen klassiek `<script>` nodig.
- `keycloak.js` importeert `./deprecations.js`. `keycloak-dpop.js` wordt enkel dynamisch geladen als DPoP aan staat. Wij gebruiken DPoP niet, maar het bestand zit mee zodat de library compleet is.
- Het pakket heeft geen runtime-dependencies.
- Niets in dit pakket is aangepast.

### Updaten

1. `npm pack keycloak-js@<versie>` in een lege map, uitpakken en `package/lib/*.js` plus `LICENSE.txt` vergelijken met deze bestanden.
2. Vier bestanden vervangen, hashes in dit bestand bijwerken en de versie hierboven aanpassen.
3. Opnieuw testen: login, refresh en logout (zie J9).
